using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Учёт фактических расходов: ввод, корректировка, сверка с первичным документом.
/// Все изменения разрешены только в незакрытом отчётном периоде.
/// </summary>
public sealed class ExpenseService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ExpenseService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<ActualExpenseDto>> GetAllAsync(
        int? periodId, int? projectId, int? expenseItemId, string? status,
        SessionInfo session, CancellationToken cancellationToken)
    {
        var query = _db.ActualExpenses
            .Include(a => a.Period)
            .Include(a => a.ExpenseItem).ThenInclude(i => i.Smeta).ThenInclude(s => s.Project)
            .AsQueryable();

        if (periodId.HasValue) query = query.Where(a => a.PeriodId == periodId.Value);
        if (projectId.HasValue) query = query.Where(a => a.ExpenseItem.Smeta.ProjectId == projectId.Value);
        if (expenseItemId.HasValue) query = query.Where(a => a.ExpenseItemId == expenseItemId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);

        if (session.RoleName == "Руководитель отдела" && session.DepartmentId.HasValue)
        {
            query = query.Where(a => a.ExpenseItem.Smeta.Project.DepartmentId == session.DepartmentId.Value);
        }

        var list = await query.OrderByDescending(a => a.Date).Take(1000).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<ActualExpenseDto> CreateAsync(CreateActualExpenseRequest request, int userId, CancellationToken cancellationToken)
    {
        var item = await _db.ExpenseItems
            .Include(i => i.Smeta).ThenInclude(s => s.Project)
            .FirstOrDefaultAsync(i => i.Id == request.ExpenseItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Статья расходов не найдена.");

        ValidateAmount(request.Amount);
        if (string.IsNullOrWhiteSpace(request.DocumentType))
            throw new ArgumentException("Тип документа обязателен.");
        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
            throw new ArgumentException("Номер документа обязателен.");

        var date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        var docDate = DateTime.SpecifyKind(request.DocumentDate, DateTimeKind.Utc);

        var period = await GetOrCreatePeriodAsync(date, cancellationToken);
        if (period.IsClosed)
        {
            throw new InvalidOperationException($"Отчётный период {period.Year:D4}/{period.Month:D2} закрыт. Ввод расходов запрещён.");
        }

        // Сумма уже введённых расходов по статье + новая не должна превышать лимит.
        // (мягкая проверка — только предупреждение в API не возвращаем, оставляем на этап контроля лимитов)
        var entity = new ActualExpense
        {
            ExpenseItemId = item.Id,
            Date = date,
            Amount = request.Amount,
            DocumentType = request.DocumentType.Trim(),
            DocumentNumber = request.DocumentNumber.Trim(),
            DocumentDate = docDate,
            Status = "Введён",
            PeriodId = period.Id
        };

        _db.ActualExpenses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Create", "ActualExpense", entity.Id.ToString(),
            $"Введён расход {entity.Amount:N2} BYN по статье {item.Name} (проект {item.Smeta.Project.Code})", cancellationToken);

        entity.Period = period;
        entity.ExpenseItem = item;
        return ToDto(entity);
    }

    public async Task<ActualExpenseDto> UpdateAsync(int id, UpdateActualExpenseRequest request, int userId, CancellationToken cancellationToken)
    {
        var entity = await _db.ActualExpenses
            .Include(a => a.Period)
            .Include(a => a.ExpenseItem).ThenInclude(i => i.Smeta).ThenInclude(s => s.Project)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Фактический расход не найден.");

        if (entity.Period.IsClosed)
        {
            throw new InvalidOperationException($"Отчётный период {entity.Period.Year:D4}/{entity.Period.Month:D2} закрыт. Корректировка запрещена.");
        }

        ValidateAmount(request.Amount);
        if (string.IsNullOrWhiteSpace(request.DocumentType))
            throw new ArgumentException("Тип документа обязателен.");
        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
            throw new ArgumentException("Номер документа обязателен.");

        var newDate = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        var newPeriod = await GetOrCreatePeriodAsync(newDate, cancellationToken);
        if (newPeriod.IsClosed)
        {
            throw new InvalidOperationException($"Новый отчётный период {newPeriod.Year:D4}/{newPeriod.Month:D2} закрыт.");
        }

        var oldAmount = entity.Amount;
        var oldStatus = entity.Status;

        entity.Date = newDate;
        entity.Amount = request.Amount;
        entity.DocumentType = request.DocumentType.Trim();
        entity.DocumentNumber = request.DocumentNumber.Trim();
        entity.DocumentDate = DateTime.SpecifyKind(request.DocumentDate, DateTimeKind.Utc);
        entity.PeriodId = newPeriod.Id;
        // Любая корректировка сбрасывает отметку сверки.
        entity.Status = "Скорректирован";

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Update", "ActualExpense", entity.Id.ToString(),
            $"Корректировка расхода: сумма {oldAmount:N2} → {entity.Amount:N2}, статус {oldStatus} → {entity.Status}", cancellationToken);

        entity.Period = newPeriod;
        return ToDto(entity);
    }

    public async Task<ActualExpenseDto> VerifyAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var entity = await _db.ActualExpenses
            .Include(a => a.Period)
            .Include(a => a.ExpenseItem).ThenInclude(i => i.Smeta).ThenInclude(s => s.Project)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Фактический расход не найден.");

        if (entity.Period.IsClosed)
        {
            throw new InvalidOperationException("Период закрыт, сверка невозможна.");
        }

        if (entity.Status == "Сверен")
        {
            return ToDto(entity);
        }

        entity.Status = "Сверен";
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Verify", "ActualExpense", entity.Id.ToString(),
            $"Сверка расхода {entity.Id} с документом {entity.DocumentType} № {entity.DocumentNumber}", cancellationToken);

        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var entity = await _db.ActualExpenses
            .Include(a => a.Period)
            .Include(a => a.ExpenseItem).ThenInclude(i => i.Smeta).ThenInclude(s => s.Project)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Фактический расход не найден.");

        if (entity.Period.IsClosed)
        {
            throw new InvalidOperationException(
                $"Отчётный период {entity.Period.Year:D4}/{entity.Period.Month:D2} закрыт. Удаление расходов запрещено.");
        }

        var amount = entity.Amount;
        var itemName = entity.ExpenseItem?.Name ?? string.Empty;
        var projectCode = entity.ExpenseItem?.Smeta?.Project?.Code ?? string.Empty;

        _db.ActualExpenses.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Delete", "ActualExpense", id.ToString(),
            $"Удалён расход {amount:N2} BYN по статье «{itemName}» (проект {projectCode})", cancellationToken);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Сумма расхода должна быть положительной.");
        if (Math.Round(amount, 2) != amount)
            throw new ArgumentException("Сумма должна содержать не более 2 знаков после запятой.");
    }

    private async Task<Period> GetOrCreatePeriodAsync(DateTime date, CancellationToken cancellationToken)
    {
        var year = date.Year;
        var month = date.Month;
        var period = await _db.Periods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, cancellationToken);
        if (period != null)
        {
            return period;
        }

        period = new Period { Year = year, Month = month, IsClosed = false, ClosedAt = null };
        _db.Periods.Add(period);
        await _db.SaveChangesAsync(cancellationToken);
        return period;
    }

    public static ActualExpenseDto ToDto(ActualExpense a) => new()
    {
        Id = a.Id,
        ExpenseItemId = a.ExpenseItemId,
        ExpenseItemName = a.ExpenseItem?.Name ?? string.Empty,
        KosguCode = a.ExpenseItem?.KosguCode ?? string.Empty,
        ProjectId = a.ExpenseItem?.Smeta?.ProjectId ?? 0,
        ProjectCode = a.ExpenseItem?.Smeta?.Project?.Code ?? string.Empty,
        ProjectName = a.ExpenseItem?.Smeta?.Project?.Name ?? string.Empty,
        SmetaId = a.ExpenseItem?.SmetaId ?? 0,
        PeriodId = a.PeriodId,
        PeriodYear = a.Period?.Year ?? 0,
        PeriodMonth = a.Period?.Month ?? 0,
        Date = a.Date,
        Amount = a.Amount,
        DocumentType = a.DocumentType,
        DocumentNumber = a.DocumentNumber,
        DocumentDate = a.DocumentDate,
        Status = a.Status
    };
}

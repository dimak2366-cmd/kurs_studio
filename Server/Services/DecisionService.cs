using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Управленческие решения директора с привязкой к статье расходов и периоду.
/// </summary>
public sealed class DecisionService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public DecisionService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<DecisionDto>> GetAllAsync(int? periodId, int? expenseItemId, CancellationToken cancellationToken)
    {
        var query = _db.ManagementDecisions
            .Include(d => d.ExpenseItem)
            .Include(d => d.Period)
            .AsQueryable();

        if (periodId.HasValue) query = query.Where(d => d.PeriodId == periodId.Value);
        if (expenseItemId.HasValue) query = query.Where(d => d.ExpenseItemId == expenseItemId.Value);

        var list = await query.OrderByDescending(d => d.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<DecisionDto> CreateAsync(CreateDecisionRequest request, int userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Decision))
        {
            throw new ArgumentException("Текст решения обязателен.");
        }

        var item = await _db.ExpenseItems.FirstOrDefaultAsync(i => i.Id == request.ExpenseItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Статья расходов не найдена.");
        var period = await _db.Periods.FirstOrDefaultAsync(p => p.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Отчётный период не найден.");

        var entity = new ManagementDecision
        {
            ExpenseItemId = item.Id,
            PeriodId = period.Id,
            Decision = request.Decision.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.ManagementDecisions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Create", "ManagementDecision", entity.Id.ToString(),
            $"Решение по статье «{item.Name}» за {period.Year:D4}/{period.Month:D2}: {entity.Decision}", cancellationToken);

        entity.ExpenseItem = item;
        entity.Period = period;
        return ToDto(entity);
    }

    public static DecisionDto ToDto(ManagementDecision d) => new()
    {
        Id = d.Id,
        ExpenseItemId = d.ExpenseItemId,
        ExpenseItemName = d.ExpenseItem?.Name ?? string.Empty,
        KosguCode = d.ExpenseItem?.KosguCode ?? string.Empty,
        PeriodId = d.PeriodId,
        PeriodYear = d.Period?.Year ?? 0,
        PeriodMonth = d.Period?.Month ?? 0,
        Decision = d.Decision,
        CreatedAt = d.CreatedAt
    };
}

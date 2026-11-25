using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Бизнес-логика работы со сметами: создание, редактирование плановых сумм,
/// утверждение, отклонение, формирование изменения к утверждённой смете.
/// </summary>
public sealed class SmetaService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public SmetaService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<SmetaDto>> GetAllAsync(int? projectId, int? year, string? status, SessionInfo session, CancellationToken cancellationToken)
    {
        var query = _db.Smetas.Include(s => s.Project).Include(s => s.ExpenseItems).ThenInclude(i => i.ActualExpenses).AsQueryable();

        if (projectId.HasValue) query = query.Where(s => s.ProjectId == projectId.Value);
        if (year.HasValue) query = query.Where(s => s.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);

        if (session.RoleName == "Руководитель отдела")
        {
            if (!session.DepartmentId.HasValue)
                throw new ForbiddenException("Не удалось определить отдел пользователя.");
            var deptId = session.DepartmentId.Value;
            query = query.Where(s => s.Project!.DepartmentId == deptId);
        }

        var list = await query.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<SmetaDetailDto> GetByIdAsync(int id, SessionInfo session, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaAsync(id, cancellationToken);
        EnsureCanView(session, smeta);
        return ToDetailDto(smeta);
    }

    /// <summary>
    /// Все статьи расходов всех смет проекта одним запросом (исправление N+1 на клиенте).
    /// Проверка прав: руководитель отдела видит только статьи своего отдела.
    /// </summary>
    public async Task<List<ExpenseItemDto>> GetItemsByProjectAsync(
        int projectId, SessionInfo session, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.DepartmentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException("Проект не найден.");
        }

        if (session.RoleName == "Руководитель отдела")
        {
            if (!session.DepartmentId.HasValue)
                throw new ForbiddenException("Не удалось определить отдел пользователя.");
            if (project.DepartmentId != session.DepartmentId.Value)
                throw new ForbiddenException("Нет прав на просмотр статей этого проекта.");
        }

        return await _db.ExpenseItems
            .Where(i => i.Smeta.ProjectId == projectId)
            .OrderBy(i => i.KosguCode)
            .ThenBy(i => i.Name)
            .Select(i => new ExpenseItemDto
            {
                Id = i.Id,
                Name = i.Name,
                KosguCode = i.KosguCode,
                PlanAmount = i.PlanAmount,
                LimitAmount = i.LimitAmount,
                Quantity = i.Quantity,
                Price = i.Price,
                FactAmount = i.ActualExpenses.Sum(a => a.Amount)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SmetaDetailDto> CreateAsync(CreateSmetaRequest request, int userId, CancellationToken cancellationToken)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw new KeyNotFoundException("Проект не найден.");
        }

        if (string.IsNullOrWhiteSpace(request.FormCode))
        {
            throw new ArgumentException("Код формы обязателен.");
        }

        if (request.Items.Count == 0)
        {
            throw new ArgumentException("Смета должна содержать хотя бы одну статью.");
        }

        if (await _db.Smetas.AnyAsync(s => s.ProjectId == request.ProjectId
            && s.FormCode == request.FormCode && s.Year == request.Year
            && s.ParentSmetaId == null, cancellationToken))
        {
            throw new InvalidOperationException($"Смета по форме {request.FormCode} за {request.Year} год для этого проекта уже существует.");
        }

        ValidateItems(request.Items);

        var smeta = new Smeta
        {
            ProjectId = request.ProjectId,
            FormCode = request.FormCode.Trim(),
            Year = request.Year,
            Status = "На утверждении",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            smeta.ExpenseItems.Add(new ExpenseItem
            {
                Name = item.Name.Trim(),
                KosguCode = item.KosguCode.Trim(),
                Quantity = item.Quantity,
                Price = item.Price,
                PlanAmount = item.Quantity * item.Price,
                LimitAmount = item.LimitAmount
            });
        }

        _db.Smetas.Add(smeta);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Create", "Smeta", smeta.Id.ToString(),
            $"Создана смета {smeta.FormCode} на {smeta.Year} год для проекта #{smeta.ProjectId}", cancellationToken);

        return await GetByIdAsync(smeta.Id, new SessionInfo { RoleName = "Главный экономист" }, cancellationToken);
    }

    public async Task<SmetaDetailDto> UpdatePlanAmountsAsync(int id, UpdateSmetaPlanRequest request, int userId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaAsync(id, cancellationToken);

        if (smeta.Status == "Утверждена" || smeta.Status == "Закрыта")
        {
            throw new InvalidOperationException("Нельзя редактировать утверждённую или закрытую смету. Используйте процедуру изменения к смете.");
        }

        var dict = smeta.ExpenseItems.ToDictionary(i => i.Id);
        foreach (var itemReq in request.Items)
        {
            if (!dict.TryGetValue(itemReq.Id, out var item))
            {
                throw new ArgumentException($"Статья с id {itemReq.Id} не принадлежит смете #{id}.");
            }

            if (itemReq.PlanAmount < 0 || itemReq.LimitAmount < 0 || itemReq.Quantity < 0 || itemReq.Price < 0)
            {
                throw new ArgumentException("Суммы и количества не могут быть отрицательными.");
            }

            // Изменение лимита требует повторного согласования директором.
            if (item.LimitAmount != itemReq.LimitAmount)
            {
                item.LimitStatus = "Предложен";
                item.LimitReturnComment = null;
            }

            item.Quantity = itemReq.Quantity;
            item.Price = itemReq.Price;
            item.LimitAmount = itemReq.LimitAmount;
            item.PlanAmount = item.Quantity * item.Price;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(userId, "UpdatePlan", "Smeta", id.ToString(),
            $"Изменены плановые суммы сметы #{id}", cancellationToken);

        return await GetByIdAsync(id, new SessionInfo { RoleName = "Главный экономист" }, cancellationToken);
    }

    public async Task<SmetaDetailDto> ApproveAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaAsync(id, cancellationToken);

        if (smeta.Status != "На утверждении")
        {
            throw new InvalidOperationException($"Утвердить можно только смету в статусе «На утверждении». Текущий статус: «{smeta.Status}».");
        }

        if (smeta.ExpenseItems.Count == 0)
        {
            throw new InvalidOperationException("Нельзя утвердить смету без статей расходов.");
        }

        // Смету нельзя утвердить, пока хотя бы один лимит не согласован директором.
        var pendingLimits = smeta.ExpenseItems.Count(i => i.LimitStatus == "Предложен");
        if (pendingLimits > 0)
        {
            throw new InvalidOperationException(
                $"Нельзя утвердить смету: {pendingLimits} статей имеют статус лимита «Предложен». Согласуйте лимиты перед утверждением.");
        }

        smeta.Status = "Утверждена";
        smeta.ApprovedAt = DateTime.UtcNow;
        smeta.RejectReason = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Approve", "Smeta", id.ToString(),
            $"Утверждена смета #{id} ({smeta.FormCode}, {smeta.Year})", cancellationToken);

        return await GetByIdAsync(id, new SessionInfo { RoleName = "Директор" }, cancellationToken);
    }

    public async Task<SmetaDetailDto> RejectAsync(int id, string reason, int userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Причина отклонения обязательна.");
        }

        var smeta = await LoadSmetaAsync(id, cancellationToken);
        if (smeta.Status != "На утверждении")
        {
            throw new InvalidOperationException($"Отклонить можно только смету в статусе «На утверждении». Текущий статус: «{smeta.Status}».");
        }

        smeta.Status = "Отклонена";
        smeta.RejectReason = reason.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Reject", "Smeta", id.ToString(),
            $"Отклонена смета #{id}. Причина: {reason}", cancellationToken);

        return await GetByIdAsync(id, new SessionInfo { RoleName = "Директор" }, cancellationToken);
    }

    public async Task<SmetaDetailDto> CreateChangeAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var original = await LoadSmetaAsync(id, cancellationToken);

        if (original.Status != "Утверждена")
        {
            throw new InvalidOperationException("Изменение можно сформировать только к утверждённой смете.");
        }

        var change = new Smeta
        {
            ProjectId = original.ProjectId,
            ParentSmetaId = original.Id,
            FormCode = original.FormCode + "-ИЗМ",
            Year = original.Year,
            Status = "На утверждении",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in original.ExpenseItems)
        {
            change.ExpenseItems.Add(new ExpenseItem
            {
                Name = item.Name,
                KosguCode = item.KosguCode,
                Quantity = item.Quantity,
                Price = item.Price,
                PlanAmount = item.Quantity * item.Price,
                LimitAmount = item.LimitAmount
            });
        }

        _db.Smetas.Add(change);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "CreateChange", "Smeta", change.Id.ToString(),
            $"Сформировано изменение к смете #{id} → смета #{change.Id}", cancellationToken);

        return await GetByIdAsync(change.Id, new SessionInfo { RoleName = "Главный экономист" }, cancellationToken);
    }

    public async Task DeleteAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var smeta = await _db.Smetas.Include(s => s.Changes).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Смета не найдена.");

        if (smeta.Status == "Утверждена" || smeta.Status == "Закрыта")
        {
            throw new InvalidOperationException("Нельзя удалить утверждённую или закрытую смету.");
        }

        if (smeta.Changes.Any())
        {
            throw new InvalidOperationException("Нельзя удалить смету, у которой есть производные изменения.");
        }

        _db.Smetas.Remove(smeta);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Delete", "Smeta", id.ToString(),
            $"Удалена смета #{id}", cancellationToken);
    }

    private async Task<Smeta> LoadSmetaAsync(int id, CancellationToken cancellationToken)
    {
        var smeta = await _db.Smetas
            .Include(s => s.Project)
            .Include(s => s.ExpenseItems).ThenInclude(i => i.ActualExpenses)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return smeta ?? throw new KeyNotFoundException("Смета не найдена.");
    }

    private static void ValidateItems(List<CreateSmetaItemRequest> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.KosguCode))
            {
                throw new ArgumentException("У каждой статьи должны быть заполнены Name и KosguCode.");
            }

            if (item.PlanAmount < 0 || item.LimitAmount < 0 || item.Quantity < 0 || item.Price < 0)
            {
                throw new ArgumentException("Суммы и количества не могут быть отрицательными.");
            }
        }
    }

    private static void EnsureCanView(SessionInfo session, Smeta smeta)
    {
        if (session.RoleName == "Руководитель отдела")
        {
            if (!session.DepartmentId.HasValue)
                throw new ForbiddenException("Не удалось определить отдел пользователя.");
            if (smeta.Project.DepartmentId != session.DepartmentId.Value)
                throw new ForbiddenException("Нет прав на просмотр смет этого отдела.");
        }
    }

    public static SmetaDto ToDto(Smeta s)
    {
        var totalPlan = s.ExpenseItems.Sum(i => i.PlanAmount);
        var totalLimit = s.ExpenseItems.Sum(i => i.LimitAmount);
        var totalFact = s.ExpenseItems.SelectMany(i => i.ActualExpenses).Sum(a => a.Amount);

        return new SmetaDto
        {
            Id = s.Id,
            ProjectId = s.ProjectId,
            ProjectCode = s.Project?.Code ?? string.Empty,
            ProjectName = s.Project?.Name ?? string.Empty,
            ParentSmetaId = s.ParentSmetaId,
            FormCode = s.FormCode,
            Year = s.Year,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            ApprovedAt = s.ApprovedAt,
            RejectReason = s.RejectReason,
            TotalPlan = totalPlan,
            TotalLimit = totalLimit,
            TotalFact = totalFact
        };
    }

    public static SmetaDetailDto ToDetailDto(Smeta s)
    {
        var dto = new SmetaDetailDto();
        var baseDto = ToDto(s);
        dto.Id = baseDto.Id;
        dto.ProjectId = baseDto.ProjectId;
        dto.ProjectCode = baseDto.ProjectCode;
        dto.ProjectName = baseDto.ProjectName;
        dto.ParentSmetaId = baseDto.ParentSmetaId;
        dto.FormCode = baseDto.FormCode;
        dto.Year = baseDto.Year;
        dto.Status = baseDto.Status;
        dto.CreatedAt = baseDto.CreatedAt;
        dto.ApprovedAt = baseDto.ApprovedAt;
        dto.RejectReason = baseDto.RejectReason;
        dto.TotalPlan = baseDto.TotalPlan;
        dto.TotalLimit = baseDto.TotalLimit;
        dto.TotalFact = baseDto.TotalFact;

        dto.Items = s.ExpenseItems.Select(i => new ExpenseItemDto
        {
            Id = i.Id,
            Name = i.Name,
            KosguCode = i.KosguCode,
            PlanAmount = i.PlanAmount,
            LimitAmount = i.LimitAmount,
            Quantity = i.Quantity,
            Price = i.Price,
            FactAmount = i.ActualExpenses.Sum(a => a.Amount)
        }).ToList();

        return dto;
    }
}

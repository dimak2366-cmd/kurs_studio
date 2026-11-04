using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Заявки на расход: подача (Руководитель отдела), рассмотрение (Главный экономист).
/// </summary>
public sealed class RequestService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public RequestService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<ExpenseRequestDto>> GetAllAsync(string? status, int? projectId, SessionInfo session, CancellationToken cancellationToken)
    {
        var query = _db.ExpenseRequests
            .Include(r => r.Project)
            .Include(r => r.ExpenseItem)
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        if (projectId.HasValue) query = query.Where(r => r.ProjectId == projectId.Value);

        // Руководитель отдела видит только свои заявки и по своему отделу.
        if (session.RoleName == "Руководитель отдела")
        {
            query = query.Where(r =>
                r.UserId == session.UserId ||
                (session.DepartmentId.HasValue && r.Project.DepartmentId == session.DepartmentId.Value));
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<ExpenseRequestDto> CreateAsync(CreateRequestRequest request, SessionInfo session, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Сумма заявки должна быть положительной.");
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Проект не найден.");

        if (session.RoleName == "Руководитель отдела" && session.DepartmentId.HasValue
            && project.DepartmentId != session.DepartmentId.Value)
        {
            throw new UnauthorizedAccessException("Нельзя подать заявку по проекту другого отдела.");
        }

        var item = await _db.ExpenseItems
            .Include(i => i.Smeta)
            .FirstOrDefaultAsync(i => i.Id == request.ExpenseItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Статья расходов не найдена.");

        if (item.Smeta.ProjectId != project.Id)
        {
            throw new ArgumentException("Статья расходов относится к другому проекту.");
        }

        var entity = new ExpenseRequest
        {
            ProjectId = project.Id,
            ExpenseItemId = item.Id,
            UserId = session.UserId,
            Amount = request.Amount,
            Status = "На рассмотрении",
            Comment = request.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.ExpenseRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(session.UserId, "Create", "ExpenseRequest", entity.Id.ToString(),
            $"Заявка на {entity.Amount:N2} BYN по статье «{item.Name}» (проект {project.Code})", cancellationToken);

        entity.Project = project;
        entity.ExpenseItem = item;
        return ToDto(entity);
    }

    public async Task<ExpenseRequestDto> ApproveAsync(int id, string? comment, int userId, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        if (entity.Status != "На рассмотрении")
        {
            throw new InvalidOperationException($"Согласовать можно только заявку в статусе «На рассмотрении». Текущий статус: «{entity.Status}».");
        }

        entity.Status = "Согласована";
        if (!string.IsNullOrWhiteSpace(comment))
        {
            entity.Comment = comment.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(userId, "Approve", "ExpenseRequest", id.ToString(),
            $"Согласована заявка #{id}", cancellationToken);

        return ToDto(entity);
    }

    public async Task<ExpenseRequestDto> RejectAsync(int id, string? comment, int userId, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        if (entity.Status != "На рассмотрении")
        {
            throw new InvalidOperationException($"Отклонить можно только заявку в статусе «На рассмотрении». Текущий статус: «{entity.Status}».");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Причина отклонения (комментарий) обязательна.");
        }

        entity.Status = "Отклонена";
        entity.Comment = comment.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Reject", "ExpenseRequest", id.ToString(),
            $"Отклонена заявка #{id}. Причина: {comment}", cancellationToken);

        return ToDto(entity);
    }

    private async Task<ExpenseRequest> LoadAsync(int id, CancellationToken cancellationToken) =>
        await _db.ExpenseRequests
            .Include(r => r.Project)
            .Include(r => r.ExpenseItem)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Заявка не найдена.");

    public static ExpenseRequestDto ToDto(ExpenseRequest r) => new()
    {
        Id = r.Id,
        ProjectId = r.ProjectId,
        ProjectCode = r.Project?.Code ?? string.Empty,
        ProjectName = r.Project?.Name ?? string.Empty,
        ExpenseItemId = r.ExpenseItemId,
        ExpenseItemName = r.ExpenseItem?.Name ?? string.Empty,
        KosguCode = r.ExpenseItem?.KosguCode ?? string.Empty,
        UserId = r.UserId,
        UserLogin = r.User?.Login ?? string.Empty,
        Amount = r.Amount,
        Status = r.Status,
        Comment = r.Comment,
        CreatedAt = r.CreatedAt
    };
}

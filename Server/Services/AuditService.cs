using Server.Data;
using Server.Models;

namespace Server.Services;

/// <summary>
/// Сервис аудита — записывает в журнал действий сведения о том,
/// кто и когда что сделал.
/// </summary>
public sealed class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        int? userId,
        string action,
        string entity,
        string entityId,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            Details = details
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

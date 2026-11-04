using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Security;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/logs")]
[AuthorizeRole("Администратор")]
public class LogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LogsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Журнал действий. Фильтры: userId, entity, action, from, to.</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit(
        [FromQuery] int? userId,
        [FromQuery] string? entity,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.Include(a => a.User).AsQueryable();

        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (from.HasValue) query = query.Where(a => a.Timestamp >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
        if (to.HasValue) query = query.Where(a => a.Timestamp <= DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));

        var list = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(500)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserLogin = a.User != null ? a.User.Login : string.Empty,
                Action = a.Action,
                Entity = a.Entity,
                EntityId = a.EntityId,
                Timestamp = a.Timestamp,
                Details = a.Details
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>Журнал ошибок. Фильтры: level, from, to.</summary>
    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors(
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = _db.ErrorLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(e => e.Level == level);
        if (from.HasValue) query = query.Where(e => e.Timestamp >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
        if (to.HasValue) query = query.Where(e => e.Timestamp <= DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));

        var list = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(500)
            .Select(e => new ErrorLogDto
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                Level = e.Level,
                Message = e.Message,
                StackTrace = e.StackTrace
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }
}

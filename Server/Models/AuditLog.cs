namespace Server.Models;

/// <summary>
/// Журнал действий пользователей (кто, когда, что сделал).
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }

    public User? User { get; set; }
}

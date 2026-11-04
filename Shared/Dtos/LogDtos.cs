namespace Shared.Dtos;

public class AuditLogDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string UserLogin { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

public class ErrorLogDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

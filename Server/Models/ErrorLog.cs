namespace Server.Models;

/// <summary>
/// Журнал ошибок сервера (уровень, сообщение, стек вызовов).
/// </summary>
public class ErrorLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

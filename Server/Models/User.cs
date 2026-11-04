namespace Server.Models;

/// <summary>
/// Учётная запись пользователя. Пароль хранится в виде SHA-256-хеша с солью.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsBlocked { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }

    public Role Role { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<ExpenseRequest> ExpenseRequests { get; set; } = new List<ExpenseRequest>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<AnalyticReport> CreatedReports { get; set; } = new List<AnalyticReport>();
}

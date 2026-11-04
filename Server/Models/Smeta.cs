namespace Server.Models;

/// <summary>
/// Смета проекта по форме Министерства финансов.
/// Если смета является изменением к утверждённой, поле ParentSmetaId указывает на оригинал.
/// Статусы: «На утверждении», «Утверждена», «Отклонена», «Закрыта».
/// </summary>
public class Smeta
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? ParentSmetaId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }

    public Project Project { get; set; } = null!;
    public Smeta? ParentSmeta { get; set; }
    public ICollection<Smeta> Changes { get; set; } = new List<Smeta>();
    public ICollection<ExpenseItem> ExpenseItems { get; set; } = new List<ExpenseItem>();
    public ICollection<AnalyticReport> AnalyticReports { get; set; } = new List<AnalyticReport>();
}

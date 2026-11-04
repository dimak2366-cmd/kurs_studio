namespace Server.Models;

/// <summary>
/// Заявка на расход, поданная руководителем отдела и рассматриваемая главным экономистом.
/// Статус: «На рассмотрении», «Согласована», «Отклонена».
/// </summary>
public class ExpenseRequest
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ExpenseItemId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ExpenseItem ExpenseItem { get; set; } = null!;
    public User User { get; set; } = null!;
}

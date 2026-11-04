namespace Server.Models;

/// <summary>
/// Управленческое решение директора, привязанное к статье расходов и периоду.
/// </summary>
public class ManagementDecision
{
    public int Id { get; set; }
    public int ExpenseItemId { get; set; }
    public int PeriodId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ExpenseItem ExpenseItem { get; set; } = null!;
    public Period Period { get; set; } = null!;
}

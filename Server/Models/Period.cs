namespace Server.Models;

/// <summary>
/// Отчётный период (год-месяц). Закрытый период запрещает ввод фактических расходов.
/// </summary>
public class Period
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<ActualExpense> ActualExpenses { get; set; } = new List<ActualExpense>();
    public ICollection<ManagementDecision> ManagementDecisions { get; set; } = new List<ManagementDecision>();
}

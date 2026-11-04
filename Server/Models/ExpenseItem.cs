namespace Server.Models;

/// <summary>
/// Статья расходов сметы (Код КОСГУ, плановая сумма, лимит, количество, цена).
/// </summary>
public class ExpenseItem
{
    public int Id { get; set; }
    public int SmetaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }

    public Smeta Smeta { get; set; } = null!;
    public ICollection<ActualExpense> ActualExpenses { get; set; } = new List<ActualExpense>();
    public ICollection<ExpenseRequest> ExpenseRequests { get; set; } = new List<ExpenseRequest>();
    public ICollection<ManagementDecision> ManagementDecisions { get; set; } = new List<ManagementDecision>();
}

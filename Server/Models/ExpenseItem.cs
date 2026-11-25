namespace Server.Models;

/// <summary>
/// Статья расходов сметы (Код КОСГУ, плановая сумма, лимит, количество, цена,
/// статус согласования лимита и комментарий возврата).
/// </summary>
public class ExpenseItem
{
    public int Id { get; set; }
    public int SmetaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; } // производное от Quantity × Price, кэшируется
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string LimitStatus { get; set; } = "Предложен";
    public string? LimitReturnComment { get; set; }

    public Smeta Smeta { get; set; } = null!;
    public ICollection<ActualExpense> ActualExpenses { get; set; } = new List<ActualExpense>();
    public ICollection<ExpenseRequest> ExpenseRequests { get; set; } = new List<ExpenseRequest>();
    public ICollection<ManagementDecision> ManagementDecisions { get; set; } = new List<ManagementDecision>();
}

namespace Shared.Dtos;

public class DecisionDto
{
    public int Id { get; set; }
    public int ExpenseItemId { get; set; }
    public string ExpenseItemName { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public int PeriodId { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateDecisionRequest
{
    public int ExpenseItemId { get; set; }
    public int PeriodId { get; set; }
    public string Decision { get; set; } = string.Empty;
}

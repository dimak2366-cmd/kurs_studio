namespace Shared.Dtos;

public class SetLimitItemRequest
{
    public int ExpenseItemId { get; set; }
    public decimal LimitAmount { get; set; }
}

public class SetLimitsRequest
{
    public List<SetLimitItemRequest> Items { get; set; } = new();
}

public class LimitCheckItemDto
{
    public int ExpenseItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal FactAmount { get; set; }
    public decimal Remaining { get; set; }
    public bool Exceeds { get; set; }
    public decimal ExcessAmount { get; set; }
}

public class LimitCheckDto
{
    public int SmetaId { get; set; }
    public bool HasExcess { get; set; }
    public List<LimitCheckItemDto> Items { get; set; } = new();
}

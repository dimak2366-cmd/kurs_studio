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
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal FactAmount { get; set; }
    public decimal Remaining { get; set; }
    public bool Exceeds { get; set; }
    public decimal ExcessAmount { get; set; }
    public string LimitStatus { get; set; } = "Предложен";
    public string? LimitReturnComment { get; set; }
}

public class LimitCheckDto
{
    public int SmetaId { get; set; }
    public bool HasExcess { get; set; }
    public List<LimitCheckItemDto> Items { get; set; } = new();
}

public class LimitActionResultDto
{
    public int SmetaId { get; set; }
    public int AffectedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public LimitCheckDto? Check { get; set; }
}

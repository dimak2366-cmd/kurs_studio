namespace Shared.Dtos;

public class ExpenseItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal FactAmount { get; set; }
    public string LimitStatus { get; set; } = "Предложен";
    public string? LimitReturnComment { get; set; }
}

public class SmetaDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int? ParentSmetaId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }
    public decimal TotalPlan { get; set; }
    public decimal TotalLimit { get; set; }
    public decimal TotalFact { get; set; }
}

public class SmetaDetailDto : SmetaDto
{
    public List<ExpenseItemDto> Items { get; set; } = new();
}

public class CreateSmetaItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public class CreateSmetaRequest
{
    public int ProjectId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<CreateSmetaItemRequest> Items { get; set; } = new();
}

public class UpdateSmetaItemPlanRequest
{
    public int Id { get; set; }
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public class UpdateSmetaPlanRequest
{
    public List<UpdateSmetaItemPlanRequest> Items { get; set; } = new();
}

public class RejectSmetaRequest
{
    public string Reason { get; set; } = string.Empty;
}

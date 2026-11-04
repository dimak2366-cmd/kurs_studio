namespace Shared.Dtos;

public class ExpenseRequestDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int ExpenseItemId { get; set; }
    public string ExpenseItemName { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserLogin { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateRequestRequest
{
    public int ProjectId { get; set; }
    public int ExpenseItemId { get; set; }
    public decimal Amount { get; set; }
    public string? Comment { get; set; }
}

public class ReviewRequestRequest
{
    public string? Comment { get; set; }
}

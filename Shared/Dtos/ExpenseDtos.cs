public class ActualExpenseDto
{
    public int Id { get; set; }
    public int ExpenseItemId { get; set; }
    public string ExpenseItemName { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int SmetaId { get; set; }
    public int PeriodId { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; }         // ← НОВОЕ
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateActualExpenseRequest
{
    public int ExpenseItemId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; } = 1m;   // ← НОВОЕ, дефолт 1
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
}

public class UpdateActualExpenseRequest
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; } = 1m;   // ← НОВОЕ
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
}

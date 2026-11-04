namespace Shared.Dtos;

public class PeriodDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int ExpenseCount { get; set; }
    public int UnverifiedCount { get; set; }
    public decimal TotalAmount { get; set; }
}

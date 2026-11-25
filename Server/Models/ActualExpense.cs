namespace Server.Models;

/// <summary>
/// Фактический (кассовый) расход по статье сметы, привязанный к первичному документу.
/// Статус: «Введён», «Сверен», «Скорректирован».
/// </summary>
public class ActualExpense
{
    public int Id { get; set; }
    public int ExpenseItemId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; }            // фактическое количество / численность
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PeriodId { get; set; }

    public ExpenseItem ExpenseItem { get; set; } = null!;
    public Period Period { get; set; } = null!;
}

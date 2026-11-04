namespace Server.Models;

/// <summary>
/// Элемент справочника статей расходов (администратор ведёт справочник,
/// главный экономист использует его при создании смет). В отличие от
/// ExpenseItem, не привязан к конкретной смете.
/// </summary>
public class CatalogExpenseItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

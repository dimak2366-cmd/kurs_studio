namespace Server.Models;

/// <summary>
/// Аналитический отчёт по смете, сформированный главным экономистом
/// и утверждаемый директором. Содержимое хранится в текстовом виде.
/// </summary>
public class AnalyticReport
{
    public int Id { get; set; }
    public int SmetaId { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public Smeta Smeta { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}

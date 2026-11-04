namespace Server.Models;

/// <summary>
/// Научно-исследовательский проект института.
/// </summary>
public class Project
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Department Department { get; set; } = null!;
    public ICollection<Smeta> Smetas { get; set; } = new List<Smeta>();
    public ICollection<ExpenseRequest> ExpenseRequests { get; set; } = new List<ExpenseRequest>();
}

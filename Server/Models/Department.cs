namespace Server.Models;

/// <summary>
/// Отдел (подразделение) научно-исследовательского института.
/// </summary>
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

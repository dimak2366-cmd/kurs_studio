namespace Shared.Dtos;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public int UserCount { get; set; }
}

public class CreateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
}

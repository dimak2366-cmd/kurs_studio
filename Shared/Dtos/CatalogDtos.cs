namespace Shared.Dtos;

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CatalogItemDto
{
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
}

public class SmetaFormDto
{
    public string FormCode { get; set; } = string.Empty;
}

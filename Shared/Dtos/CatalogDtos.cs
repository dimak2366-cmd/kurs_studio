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

public class CatalogExpenseItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateCatalogItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UpdateCatalogItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

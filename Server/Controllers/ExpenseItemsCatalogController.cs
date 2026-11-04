using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/catalog")]
public class ExpenseItemsCatalogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ExpenseItemsCatalogController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Справочник ролей.</summary>
    [HttpGet("roles")]
    [AuthorizeRole]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var list = await _db.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>Справочник статей расходов — уникальные сочетания Наименование + КОСГУ (из смет).</summary>
    [HttpGet("expense-items")]
    [AuthorizeRole]
    public async Task<IActionResult> GetExpenseItems(CancellationToken cancellationToken)
    {
        var list = await _db.ExpenseItems
            .Select(x => new CatalogItemDto { Name = x.Name, KosguCode = x.KosguCode })
            .Distinct()
            .OrderBy(x => x.KosguCode)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>Справочник форм смет (FormCode) по формам Министерства финансов.</summary>
    [HttpGet("smeta-forms")]
    [AuthorizeRole]
    public async Task<IActionResult> GetSmetaForms(CancellationToken cancellationToken)
    {
        var list = await _db.Smetas
            .Select(s => new SmetaFormDto { FormCode = s.FormCode })
            .Distinct()
            .OrderBy(s => s.FormCode)
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    // ==================== CRUD по CatalogExpenseItem (только Администратор) ====================

    /// <summary>Все элементы справочника статей (включая неактивные).</summary>
    [HttpGet("catalog-items")]
    [AuthorizeRole]
    public async Task<IActionResult> GetCatalogItems(CancellationToken cancellationToken)
    {
        var list = await _db.CatalogExpenseItems
            .OrderBy(c => c.KosguCode).ThenBy(c => c.Name)
            .Select(c => new CatalogExpenseItemDto
            {
                Id = c.Id,
                Name = c.Name,
                KosguCode = c.KosguCode,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("catalog-items/{id:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetCatalogItem(int id, CancellationToken cancellationToken)
    {
        var c = await _db.CatalogExpenseItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c == null) return NotFound(new { error = "Элемент справочника не найден." });
        return Ok(new CatalogExpenseItemDto
        {
            Id = c.Id,
            Name = c.Name,
            KosguCode = c.KosguCode,
            IsActive = c.IsActive
        });
    }

    [HttpPost("catalog-items")]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> CreateCatalogItem([FromBody] CreateCatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.KosguCode))
        {
            return BadRequest(new { error = "Поля Name и KosguCode обязательны." });
        }

        var name = request.Name.Trim();
        var code = request.KosguCode.Trim();

        if (await _db.CatalogExpenseItems.AnyAsync(c => c.Name == name && c.KosguCode == code, cancellationToken))
        {
            return Conflict(new { error = "Элемент с таким Наименованием и КОСГУ уже существует." });
        }

        var entity = new CatalogExpenseItem { Name = name, KosguCode = code, IsActive = request.IsActive };
        _db.CatalogExpenseItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(CurrentUserId(), "Create", "CatalogExpenseItem", entity.Id.ToString(),
            $"Создан элемент справочника: {entity.KosguCode} / {entity.Name}", cancellationToken);

        return CreatedAtAction(nameof(GetCatalogItem), new { id = entity.Id }, new CatalogExpenseItemDto
        {
            Id = entity.Id,
            Name = entity.Name,
            KosguCode = entity.KosguCode,
            IsActive = entity.IsActive
        });
    }

    [HttpPut("catalog-items/{id:int}")]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> UpdateCatalogItem(int id, [FromBody] UpdateCatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.KosguCode))
        {
            return BadRequest(new { error = "Поля Name и KosguCode обязательны." });
        }

        var entity = await _db.CatalogExpenseItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound(new { error = "Элемент справочника не найден." });

        var name = request.Name.Trim();
        var code = request.KosguCode.Trim();

        if (await _db.CatalogExpenseItems.AnyAsync(c => c.Id != id && c.Name == name && c.KosguCode == code, cancellationToken))
        {
            return Conflict(new { error = "Элемент с таким Наименованием и КОСГУ уже существует." });
        }

        entity.Name = name;
        entity.KosguCode = code;
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(CurrentUserId(), "Update", "CatalogExpenseItem", entity.Id.ToString(),
            $"Изменён элемент справочника: {entity.KosguCode} / {entity.Name} (IsActive={entity.IsActive})", cancellationToken);

        return Ok(new CatalogExpenseItemDto
        {
            Id = entity.Id,
            Name = entity.Name,
            KosguCode = entity.KosguCode,
            IsActive = entity.IsActive
        });
    }

    [HttpDelete("catalog-items/{id:int}")]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> DeleteCatalogItem(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.CatalogExpenseItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound(new { error = "Элемент справочника не найден." });

        _db.CatalogExpenseItems.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(CurrentUserId(), "Delete", "CatalogExpenseItem", id.ToString(),
            $"Удалён элемент справочника: {entity.KosguCode} / {entity.Name}", cancellationToken);

        return Ok(new { success = true });
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

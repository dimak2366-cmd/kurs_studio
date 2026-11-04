using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Security;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/catalog")]
public class ExpenseItemsCatalogController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExpenseItemsCatalogController(AppDbContext db)
    {
        _db = db;
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

    /// <summary>Справочник статей расходов — уникальные сочетания Наименование + КОСГУ.</summary>
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
}

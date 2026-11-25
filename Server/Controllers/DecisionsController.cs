using Microsoft.AspNetCore.Mvc;
using Server.Infrastructure;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/decisions")]
public class DecisionsController : ControllerBase
{
    private readonly DecisionService _decisions;

    public DecisionsController(DecisionService decisions)
    {
        _decisions = decisions;
    }

    [HttpGet]
    [AuthorizeRole("Директор", "Главный экономист", "Администратор")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? periodId,
        [FromQuery] int? expenseItemId,
        CancellationToken cancellationToken) =>
        Ok(await _decisions.GetAllAsync(periodId, expenseItemId, cancellationToken));

    [HttpPost]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Create([FromBody] CreateDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _decisions.CreateAsync(request, HttpContext.CurrentUserId(), cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}

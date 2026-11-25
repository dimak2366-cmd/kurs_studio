using Microsoft.AspNetCore.Mvc;
using Server.Infrastructure;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/expenses")]
public class ActualExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public ActualExpensesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? periodId,
        [FromQuery] int? projectId,
        [FromQuery] int? expenseItemId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null) return Unauthorized();
        var list = await _expenseService.GetAllAsync(periodId, projectId, expenseItemId, status, session, cancellationToken);
        return Ok(list);
    }

    [HttpPost]
    [AuthorizeRole("Бухгалтер")]
    public async Task<IActionResult> Create([FromBody] CreateActualExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _expenseService.CreateAsync(request, HttpContext.CurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [AuthorizeRole("Бухгалтер")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateActualExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenseService.UpdateAsync(id, request, HttpContext.CurrentUserId(), cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/verify")]
    [AuthorizeRole("Бухгалтер")]
    public async Task<IActionResult> Verify(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenseService.VerifyAsync(id, HttpContext.CurrentUserId(), cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [AuthorizeRole("Бухгалтер")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _expenseService.DeleteAsync(id, HttpContext.CurrentUserId(), cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

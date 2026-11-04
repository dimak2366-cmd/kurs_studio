using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/periods")]
public class PeriodsController : ControllerBase
{
    private readonly PeriodService _periodService;

    public PeriodsController(PeriodService periodService)
    {
        _periodService = periodService;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _periodService.GetAllAsync(cancellationToken));
    }

    [HttpPost("{id:int}/close")]
    [AuthorizeRole("Бухгалтер")]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int uid ? uid : 0;
            return Ok(await _periodService.CloseAsync(id, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

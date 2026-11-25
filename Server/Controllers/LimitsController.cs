using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/limits")]
public class LimitsController : ControllerBase
{
    private readonly LimitService _limits;

    public LimitsController(LimitService limits)
    {
        _limits = limits;
    }

    [HttpGet("{smetaId:int}")]
    [AuthorizeRole("Главный экономист", "Директор", "Администратор", "Бухгалтер", "Руководитель отдела")]
    public async Task<IActionResult> Get(int smetaId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _limits.GetLimitsAsync(smetaId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("{smetaId:int}")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Set(int smetaId, [FromBody] SetLimitsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int id ? id : 0;
            return Ok(await _limits.SetLimitsAsync(smetaId, request.Items, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("{smetaId:int}/check")]
    [AuthorizeRole("Главный экономист", "Директор", "Администратор", "Бухгалтер", "Руководитель отдела")]
    public async Task<IActionResult> Check(int smetaId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _limits.CheckExcessAsync(smetaId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{smetaId:int}/approve")]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Approve(int smetaId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int id ? id : 0;
            return Ok(await _limits.ApproveAsync(smetaId, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{smetaId:int}/return")]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Return(int smetaId, [FromBody] ReviewRequestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int id ? id : 0;
            return Ok(await _limits.ReturnAsync(smetaId, request?.Comment ?? string.Empty, userId, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

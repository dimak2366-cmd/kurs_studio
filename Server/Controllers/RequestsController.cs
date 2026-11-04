using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly RequestService _requests;

    public RequestsController(RequestService requests)
    {
        _requests = requests;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? projectId,
        CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null) return Unauthorized();
        return Ok(await _requests.GetAllAsync(status, projectId, session, cancellationToken));
    }

    [HttpPost]
    [AuthorizeRole("Руководитель отдела")]
    public async Task<IActionResult> Create([FromBody] CreateRequestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = (SessionInfo?)HttpContext.Items["Session"];
            if (session == null) return Unauthorized();
            return Ok(await _requests.CreateAsync(request, session, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/approve")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Approve(int id, [FromBody] ReviewRequestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int uid ? uid : 0;
            return Ok(await _requests.ApproveAsync(id, request?.Comment, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/reject")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReviewRequestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int uid ? uid : 0;
            return Ok(await _requests.RejectAsync(id, request?.Comment, userId, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

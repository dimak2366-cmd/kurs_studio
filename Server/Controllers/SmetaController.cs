using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/smeta")]
public class SmetaController : ControllerBase
{
    private readonly SmetaService _smeta;
    private readonly AuditService _audit;

    public SmetaController(SmetaService smeta, AuditService audit)
    {
        _smeta = smeta;
        _audit = audit;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? projectId,
        [FromQuery] int? year,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null) return Unauthorized();
        var list = await _smeta.GetAllAsync(projectId, year, status, session, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null) return Unauthorized();
        try
        {
            return Ok(await _smeta.GetByIdAsync(id, session, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    /// <summary>
    /// Все статьи расходов всех смет проекта одним запросом.
    /// Используется в диалоге подачи заявки, чтобы избежать N+1 на клиенте.
    /// </summary>
    [HttpGet("items/by-project/{projectId:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetItemsByProject(int projectId, CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null) return Unauthorized();
        try
        {
            return Ok(await _smeta.GetItemsByProjectAsync(projectId, session, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPost]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Create([FromBody] CreateSmetaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _smeta.CreateAsync(request, CurrentUserId() ?? 0, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdateSmetaPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _smeta.UpdatePlanAmountsAsync(id, request, CurrentUserId() ?? 0, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/approve")]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _smeta.ApproveAsync(id, CurrentUserId() ?? 0, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/reject")]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectSmetaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _smeta.RejectAsync(id, request.Reason, CurrentUserId() ?? 0, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/changes")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> CreateChange(int id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _smeta.CreateChangeAsync(id, CurrentUserId() ?? 0, cancellationToken);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _smeta.DeleteAsync(id, CurrentUserId() ?? 0, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

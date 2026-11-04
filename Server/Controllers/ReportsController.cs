using System.Text;
using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _reports.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _reports.GetByIdAsync(id, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:int}/download")]
    [AuthorizeRole]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        try
        {
            var r = await _reports.GetByIdAsync(id, cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(r.Content);
            return File(bytes, "text/csv; charset=utf-8", $"report_smeta{r.SmetaId}_{r.CreatedAt:yyyyMMdd_HHmmss}.csv");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost]
    [AuthorizeRole("Главный экономист")]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int uid ? uid : 0;
            return Ok(await _reports.CreateAsync(request.SmetaId, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/approve")]
    [AuthorizeRole("Директор")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext.Items["UserId"] is int uid ? uid : 0;
            return Ok(await _reports.ApproveAsync(id, userId, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

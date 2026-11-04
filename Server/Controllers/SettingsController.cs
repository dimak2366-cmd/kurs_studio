using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/settings")]
[AuthorizeRole("Администратор")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;
    private readonly AuditService _audit;

    public SettingsController(SettingsService settings, AuditService audit)
    {
        _settings = settings;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Get() => Ok(_settings.Get());

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _settings.UpdateAsync(request, cancellationToken);
            await _audit.LogAsync(CurrentUserId(), "Update", "Settings", "appsettings.json",
                "Изменены параметры сервера", cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

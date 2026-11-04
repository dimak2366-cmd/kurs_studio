using Microsoft.AspNetCore.Mvc;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/backup")]
[AuthorizeRole("Администратор")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backup;
    private readonly AuditService _audit;

    public BackupController(BackupService backup, AuditService audit)
    {
        _backup = backup;
        _audit = audit;
    }

    [HttpGet("list")]
    public IActionResult List() => Ok(_backup.ListBackups());

    [HttpPost("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            var file = await _backup.CreateBackupAsync(cancellationToken);
            await _audit.LogAsync(CurrentUserId(), "Backup", "Database", file.FileName,
                $"Создана резервная копия {file.FileName}", cancellationToken);
            return Ok(new BackupResultDto { Success = true, Message = "Резервная копия создана.", File = file });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { error = "Поле FileName обязательно." });
        }

        try
        {
            await _backup.RestoreAsync(request.FileName, cancellationToken);
            await _audit.LogAsync(CurrentUserId(), "Restore", "Database", request.FileName,
                $"Восстановление из {request.FileName}", cancellationToken);
            return Ok(new BackupResultDto { Success = true, Message = "База данных восстановлена." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

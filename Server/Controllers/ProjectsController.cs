using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;

    public ProjectsController(AppDbContext db, AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet]
    [AuthorizeRole]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        var query = _db.Projects.Include(p => p.Department).AsQueryable();

        // Руководитель отдела видит только свои проекты.
        if (session != null && session.RoleName == "Руководитель отдела" && session.DepartmentId.HasValue)
        {
            query = query.Where(p => p.DepartmentId == session.DepartmentId.Value);
        }

        var list = await query
            .OrderBy(p => p.Code)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.Department.Name,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                SmetaCount = p.Smetas.Count
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        var p = await _db.Projects
            .Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (p == null)
        {
            return NotFound(new { error = "Проект не найден." });
        }

        AccessGuard.EnsureDepartmentAccess(HttpContext, p.DepartmentId);

        return Ok(new ProjectDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department.Name,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            SmetaCount = await _db.Smetas.CountAsync(s => s.ProjectId == p.Id, cancellationToken)
        });
    }

    [HttpPost]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Поля Code и Name обязательны." });
        }

        if (await _db.Projects.AnyAsync(p => p.Code == request.Code.Trim(), cancellationToken))
        {
            return Conflict(new { error = "Проект с таким кодом уже существует." });
        }

        if (!await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken))
        {
            return BadRequest(new { error = "Указанный отдел не найден." });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new { error = "Дата окончания должна быть позже даты начала." });
        }

        var p = new Project
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            DepartmentId = request.DepartmentId,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc)
        };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Create", "Project", p.Id.ToString(),
            $"Создан проект {p.Code}", cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = p.Id }, new { p.Id, p.Code, p.Name });
    }

    [HttpPut("{id:int}")]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null)
        {
            return NotFound(new { error = "Проект не найден." });
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Поля Code и Name обязательны." });
        }

        if (await _db.Projects.AnyAsync(x => x.Code == request.Code.Trim() && x.Id != id, cancellationToken))
        {
            return Conflict(new { error = "Проект с таким кодом уже существует." });
        }

        if (!await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken))
        {
            return BadRequest(new { error = "Указанный отдел не найден." });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new { error = "Дата окончания должна быть позже даты начала." });
        }

        p.Code = request.Code.Trim();
        p.Name = request.Name.Trim();
        p.DepartmentId = request.DepartmentId;
        p.StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        p.EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Update", "Project", p.Id.ToString(),
            $"Изменён проект {p.Code}", cancellationToken);

        return Ok(new { p.Id, p.Code, p.Name });
    }

    [HttpDelete("{id:int}")]
    [AuthorizeRole("Администратор")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null)
        {
            return NotFound(new { error = "Проект не найден." });
        }

        if (await _db.Smetas.AnyAsync(s => s.ProjectId == id, cancellationToken))
        {
            return BadRequest(new { error = "Нельзя удалить проект, у которого есть сметы." });
        }

        _db.Projects.Remove(p);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Delete", "Project", id.ToString(),
            $"Удалён проект {p.Code}", cancellationToken);

        return Ok(new { success = true });
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

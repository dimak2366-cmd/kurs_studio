using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/departments")]
[AuthorizeRole("Администратор")]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;

    public DepartmentsController(AppDbContext db, AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var list = await _db.Departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                ProjectCount = d.Projects.Count,
                UserCount = d.Users.Count
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var d = await _db.Departments
            .Where(x => x.Id == id)
            .Select(x => new DepartmentDto
            {
                Id = x.Id,
                Name = x.Name,
                ProjectCount = x.Projects.Count,
                UserCount = x.Users.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
        return d == null ? NotFound(new { error = "Отдел не найден." }) : Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Название отдела обязательно." });
        }

        if (await _db.Departments.AnyAsync(d => d.Name == request.Name.Trim(), cancellationToken))
        {
            return Conflict(new { error = "Отдел с таким названием уже существует." });
        }

        var dep = new Department { Name = request.Name.Trim() };
        _db.Departments.Add(dep);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Create", "Department", dep.Id.ToString(),
            $"Создан отдел {dep.Name}", cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = dep.Id }, new DepartmentDto { Id = dep.Id, Name = dep.Name });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Название отдела обязательно." });
        }

        var dep = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dep == null)
        {
            return NotFound(new { error = "Отдел не найден." });
        }

        if (await _db.Departments.AnyAsync(d => d.Name == request.Name.Trim() && d.Id != id, cancellationToken))
        {
            return Conflict(new { error = "Отдел с таким названием уже существует." });
        }

        dep.Name = request.Name.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Update", "Department", dep.Id.ToString(),
            $"Изменён отдел {dep.Name}", cancellationToken);

        return Ok(new DepartmentDto { Id = dep.Id, Name = dep.Name });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var dep = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dep == null)
        {
            return NotFound(new { error = "Отдел не найден." });
        }

        var hasProjects = await _db.Projects.AnyAsync(p => p.DepartmentId == id, cancellationToken);
        var hasUsers = await _db.Users.AnyAsync(u => u.DepartmentId == id, cancellationToken);
        if (hasProjects || hasUsers)
        {
            return BadRequest(new { error = "Нельзя удалить отдел, к которому привязаны проекты или пользователи." });
        }

        _db.Departments.Remove(dep);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Delete", "Department", id.ToString(),
            $"Удалён отдел {dep.Name}", cancellationToken);

        return Ok(new { success = true });
    }

    private int? CurrentUserId() => HttpContext.Items["UserId"] is int id ? id : null;
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/users")]
[AuthorizeRole("Администратор")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;

    public UsersController(AppDbContext db, AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .OrderBy(u => u.Login)
            .Select(u => ToDto(u))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user == null ? NotFound(new { error = "Пользователь не найден." }) : Ok(ToDto(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { error = "Поля Login, Password и FullName обязательны." });
        }

        if (await _db.Users.AnyAsync(u => u.Login == request.Login, cancellationToken))
        {
            return Conflict(new { error = "Пользователь с таким логином уже существует." });
        }

        var roleExists = await _db.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);
        if (!roleExists)
        {
            return BadRequest(new { error = "Указанная роль не найдена." });
        }

        if (request.DepartmentId.HasValue)
        {
            var depExists = await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId.Value, cancellationToken);
            if (!depExists)
            {
                return BadRequest(new { error = "Указанный отдел не найден." });
            }
        }

        var salt = PasswordHasher.GenerateSalt();
        var user = new User
        {
            Login = request.Login.Trim(),
            FullName = request.FullName.Trim(),
            PasswordSalt = salt,
            PasswordHash = PasswordHasher.ComputeHash(request.Password, salt),
            RoleId = request.RoleId,
            DepartmentId = request.DepartmentId,
            IsBlocked = false,
            FailedAttempts = 0
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Create", "User", user.Id.ToString(),
            $"Создан пользователь {user.Login}", cancellationToken);

        // Перезагружаем с навигационными свойствами для ответа.
        await _db.Entry(user).Reference(u => u.Role).LoadAsync(cancellationToken);
        if (user.DepartmentId.HasValue)
        {
            await _db.Entry(user).Reference(u => u.Department).LoadAsync(cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "Пользователь не найден." });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { error = "Поле FullName обязательно." });
        }

        if (!await _db.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken))
        {
            return BadRequest(new { error = "Указанная роль не найдена." });
        }

        if (request.DepartmentId.HasValue &&
            !await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId.Value, cancellationToken))
        {
            return BadRequest(new { error = "Указанный отдел не найден." });
        }

        user.FullName = request.FullName.Trim();
        user.RoleId = request.RoleId;
        user.DepartmentId = request.DepartmentId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Update", "User", user.Id.ToString(),
            $"Изменён пользователь {user.Login}", cancellationToken);

        await _db.Entry(user).Reference(u => u.Role).LoadAsync(cancellationToken);
        if (user.DepartmentId.HasValue)
        {
            await _db.Entry(user).Reference(u => u.Department).LoadAsync(cancellationToken);
        }

        return Ok(ToDto(user));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { error = "Новый пароль должен содержать минимум 6 символов." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "Пользователь не найден." });
        }

        var salt = PasswordHasher.GenerateSalt();
        user.PasswordSalt = salt;
        user.PasswordHash = PasswordHasher.ComputeHash(request.NewPassword, salt);
        user.FailedAttempts = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "ResetPassword", "User", user.Id.ToString(),
            $"Сброшен пароль пользователя {user.Login}", cancellationToken);

        return Ok(new ResetPasswordResponse { Success = true, NewPassword = request.NewPassword });
    }

    [HttpPost("{id:int}/block")]
    public async Task<IActionResult> Block(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "Пользователь не найден." });
        }

        user.IsBlocked = true;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Block", "User", user.Id.ToString(),
            $"Заблокирован пользователь {user.Login}", cancellationToken);

        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/unblock")]
    public async Task<IActionResult> Unblock(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "Пользователь не найден." });
        }

        user.IsBlocked = false;
        user.FailedAttempts = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(CurrentUserId(), "Unblock", "User", user.Id.ToString(),
            $"Разблокирован пользователь {user.Login}", cancellationToken);

        return Ok(new { success = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "Пользователь не найден." });
        }

        try
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Нельзя удалить пользователя, у которого есть связанные записи. Используйте блокировку." });
        }

        await _auditService.LogAsync(CurrentUserId(), "Delete", "User", id.ToString(),
            $"Удалён пользователь {user.Login}", cancellationToken);

        return Ok(new { success = true });
    }

    private int? CurrentUserId() =>
        HttpContext.Items["UserId"] is int id ? id : null;

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Login = u.Login,
        FullName = u.FullName,
        RoleId = u.RoleId,
        RoleName = u.Role?.Name ?? string.Empty,
        DepartmentId = u.DepartmentId,
        DepartmentName = u.Department?.Name,
        IsBlocked = u.IsBlocked,
        FailedAttempts = u.FailedAttempts,
        LockoutUntil = u.LockoutUntil
    };
}

using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Infrastructure;
using Server.Models;
using Server.Security;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Сервис аутентификации: вход с проверкой пароля и блокировкой после N неудач,
/// выход, смена пароля. Для аудита использует AuditService.
/// </summary>
public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly AuditService _auditService;

    public AuthService(AppDbContext db, TokenService tokenService, AuditService auditService)
    {
        _db = db;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Логин и пароль обязательны.");
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Login == request.Login, cancellationToken);

        if (user == null)
        {
            // Не раскрываем, существует ли пользователь.
            throw new UnauthorizedAccessException("Неверный логин или пароль.");
        }

        if (user.IsBlocked)
        {
            throw new UnauthorizedAccessException("Учётная запись заблокирована администратором.");
        }

        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                $"Учётная запись временно заблокирована до {user.LockoutUntil.Value:yyyy-MM-dd HH:mm:ss} UTC.");
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            user.FailedAttempts++;
            string message;
            if (user.FailedAttempts >= AppConfig.Instance.MaxFailedAttempts)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(AppConfig.Instance.LockoutMinutes);
                user.FailedAttempts = 0;
                message = $"Превышено число попыток. Учётная запись заблокирована до {user.LockoutUntil:yyyy-MM-dd HH:mm:ss} UTC.";
            }
            else
            {
                var left = AppConfig.Instance.MaxFailedAttempts - user.FailedAttempts;
                message = $"Неверный логин или пароль. Осталось попыток: {left}.";
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(user.Id, "LoginFailed", "User", user.Id.ToString(),
                $"Неудачный вход с логином {user.Login}", cancellationToken);

            throw new UnauthorizedAccessException(message);
        }

        // Успешный вход — сбрасываем счётчик и блокировку.
        user.FailedAttempts = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync(cancellationToken);

        var session = _tokenService.CreateSession(user);
        await _auditService.LogAsync(user.Id, "Login", "User", user.Id.ToString(),
            $"Успешный вход с логином {user.Login}", cancellationToken);

        return new LoginResponse
        {
            Token = session.Token,
            UserId = session.UserId,
            Login = session.Login,
            FullName = session.FullName,
            RoleName = session.RoleName,
            ExpiresAt = session.ExpiresAt
        };
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        var session = _tokenService.GetSession(token);
        if (session == null)
        {
            return;
        }

        _tokenService.Revoke(token);
        await _auditService.LogAsync(session.UserId, "Logout", "User", session.UserId.ToString(),
            "Выход из системы", cancellationToken);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new ArgumentException("Новый пароль должен содержать минимум 6 символов.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Пользователь не найден.");

        if (!PasswordHasher.Verify(request.OldPassword, user.PasswordSalt, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Старый пароль введён неверно.");
        }

        var newSalt = PasswordHasher.GenerateSalt();
        user.PasswordSalt = newSalt;
        user.PasswordHash = PasswordHasher.ComputeHash(request.NewPassword, newSalt);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(userId, "ChangePassword", "User", userId.ToString(),
            "Пользователь сменил свой пароль", cancellationToken);
    }
}

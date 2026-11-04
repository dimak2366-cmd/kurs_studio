using System.Collections.Concurrent;
using Server.Infrastructure;
using Server.Models;

namespace Server.Services;

/// <summary>
/// Хранилище активных сессий в памяти сервера.
/// </summary>
public sealed class TokenService
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public SessionInfo CreateSession(User user)
    {
        var token = Guid.NewGuid().ToString("N");
        var session = new SessionInfo
        {
            Token = token,
            UserId = user.Id,
            Login = user.Login,
            FullName = user.FullName,
            RoleName = user.Role?.Name ?? string.Empty,
            DepartmentId = user.DepartmentId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(AppConfig.Instance.SessionTimeoutMinutes)
        };
        _sessions[token] = session;
        return session;
    }

    public SessionInfo? GetSession(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (!_sessions.TryGetValue(token, out var session))
        {
            return null;
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return session;
    }

    public bool Revoke(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        return _sessions.TryRemove(token, out _);
    }
}

/// <summary>Данные активной сессии.</summary>
public sealed class SessionInfo
{
    public string Token { get; init; } = string.Empty;
    public int UserId { get; init; }
    public string Login { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public int? DepartmentId { get; init; }
    public DateTime ExpiresAt { get; init; }
}

using Shared.Dtos;

namespace Client.Services;

/// <summary>
/// Хранит данные текущего пользователя и токен авторизации.
/// </summary>
public sealed class SessionService
{
    public int UserId { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public void SetSession(LoginResponse response)
    {
        UserId = response.UserId;
        Login = response.Login;
        FullName = response.FullName;
        RoleName = response.RoleName;
        Token = response.Token;
        ExpiresAt = response.ExpiresAt;
    }

    public void Clear()
    {
        UserId = 0;
        Login = string.Empty;
        FullName = string.Empty;
        RoleName = string.Empty;
        Token = string.Empty;
        ExpiresAt = default;
    }
}

namespace Shared.Dtos;

/// <summary>Запрос на вход в систему.</summary>
public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Результат успешного входа.</summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Запрос на смену пароля (доступен только текущему пользователю).</summary>
public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Обёртка для зашифрованного тела запроса. Поле Payload — base64 от AES-шифртекста.
/// Используется в /api/auth/login-encrypted для демонстрации шифрования трафика.
/// </summary>
public class EncryptedPayload
{
    public string Payload { get; set; } = string.Empty;
}

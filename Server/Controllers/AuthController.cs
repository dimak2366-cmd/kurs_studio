using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;
    private readonly IAesEncryptor _aesEncryptor;

    public AuthController(
        AuthService authService,
        TokenService tokenService,
        IAesEncryptor aesEncryptor)
    {
        _authService = authService;
        _tokenService = tokenService;
        _aesEncryptor = aesEncryptor;
    }

    /// <summary>Вход в систему. Возвращает токен и данные пользователя.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Демонстрация шифрования тела запроса. Тело — { "payload": "&lt;base64 AES&gt;" },
    /// внутри зашифрован JSON LoginRequest. Ответ не шифруется (для простоты клиента).
    /// </summary>
    [HttpPost("login-encrypted")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginEncrypted([FromBody] EncryptedPayload payload, CancellationToken cancellationToken)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Payload))
        {
            return BadRequest(new { error = "Поле payload обязательно." });
        }

        string json;
        try
        {
            json = _aesEncryptor.Decrypt(payload.Payload);
        }
        catch (CryptographicException)
        {
            return BadRequest(new { error = "Не удалось расшифровать payload." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { error = "Не удалось расшифровать payload." });
        }

        LoginRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LoginRequest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Некорректный JSON внутри payload." });
        }

        if (request == null)
        {
            return BadRequest(new { error = "Некорректный JSON внутри payload." });
        }

        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, new { error = ex.Message });
        }
    }

    /// <summary>Выход: аннулирует текущий токен.</summary>
    [HttpPost("logout")]
    [AuthorizeRole]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(new { error = "Токен не передан." });
        }

        await _authService.LogoutAsync(token, cancellationToken);
        return Ok(new { success = true });
    }

    /// <summary>Смена пароля текущего пользователя.</summary>
    [HttpPost("change-password")]
    [AuthorizeRole]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var session = (SessionInfo?)HttpContext.Items["Session"];
        if (session == null)
        {
            return Unauthorized(new { error = "Сессия не найдена." });
        }

        try
        {
            await _authService.ChangePasswordAsync(session.UserId, request, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private string? ExtractToken()
    {
        var header = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return header.Substring("Bearer ".Length).Trim();
    }
}

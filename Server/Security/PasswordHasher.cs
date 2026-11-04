using System.Security.Cryptography;
using System.Text;

namespace Server.Security;

/// <summary>
/// Генератор соли и хеширования паролей. Используется алгоритм SHA-256 с солью.
/// Для сравнения хешей применяется сравнение за постоянное время (CryptographicOperations.FixedTimeEquals).
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// Генерирует криптографически стойкую соль заданного размера (по умолчанию — 32 байта).
    /// Возвращает base64-строку.
    /// </summary>
    public static string GenerateSalt(int size = 32)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(size);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Вычисляет SHA-256-хеш от строки "соль + пароль" и возвращает base64-строку.
    /// </summary>
    public static string ComputeHash(string password, string salt)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);

        var combined = salt + password;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Проверяет соответствие пароля сохранённым соли и хешу за постоянное время.
    /// </summary>
    public static bool Verify(string password, string salt, string expectedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        var actualHash = ComputeHash(password, salt);
        var actualBytes = Encoding.UTF8.GetBytes(actualHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);

        if (actualBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}

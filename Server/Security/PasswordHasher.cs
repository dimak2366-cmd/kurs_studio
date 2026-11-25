using System.Security.Cryptography;

namespace Server.Security;

/// <summary>
/// Хеширование паролей алгоритмом PBKDF2-SHA256 (100 000 итераций, соль 16 байт, хеш 32 байта).
/// Формат хранения: "$pbkdf2-sha256$<iterations>$<base64-salt>$<base64-hash>".
/// Сравнение хешей выполняется за постоянное время через CryptographicOperations.FixedTimeEquals.
/// Поддерживает обратную совместимость со старым форматом (hex SHA-256).
/// </summary>
public static class PasswordHasher
{
    private const int DefaultIterations = 100000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string GenerateSalt()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
    }

    public static string ComputeHash(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, Convert.FromBase64String(salt), DefaultIterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);
        return $"$pbkdf2-sha256${DefaultIterations}${salt}${Convert.ToBase64String(hash)}";
    }

    public static string Hash(string password)
    {
        var salt = GenerateSalt();
        return ComputeHash(password, salt);
    }

    public static bool Verify(string password, string salt, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        if (storedHash.StartsWith("$pbkdf2-sha256$"))
        {
            var parts = storedHash.Split('$');
            if (parts.Length == 5 && int.TryParse(parts[2], out int iterations))
            {
                var expectedHash = Convert.FromBase64String(parts[4]);
                using var pbkdf2 = new Rfc2898DeriveBytes(
                    password, Convert.FromBase64String(parts[3]), iterations, HashAlgorithmName.SHA256);
                var hash = pbkdf2.GetBytes(HashSize);
                return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
            }
        }

        // Обратная совместимость со старым форматом (base64-hash без префикса, используется salt).
        using var legacyPbkdf2 = new Rfc2898DeriveBytes(
            password, Convert.FromBase64String(salt), DefaultIterations, HashAlgorithmName.SHA256);
        var legacyHash = legacyPbkdf2.GetBytes(HashSize);
        var expectedLegacyHash = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(legacyHash, expectedLegacyHash);
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        if (storedHash.StartsWith("$pbkdf2-sha256$"))
        {
            var parts = storedHash.Split('$');
            if (parts.Length == 5 && int.TryParse(parts[2], out int iterations))
            {
                try
                {
                    var expectedHash = Convert.FromBase64String(parts[4]);
                    using var pbkdf2 = new Rfc2898DeriveBytes(
                        password, Convert.FromBase64String(parts[3]), iterations, HashAlgorithmName.SHA256);
                    var hash = pbkdf2.GetBytes(HashSize);
                    return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
                }
                catch (FormatException) { return false; }
            }
        }

        // Обратная совместимость со старым форматом: hex SHA-256
        try
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            var storedBytes = Convert.FromHexString(storedHash);
            return CryptographicOperations.FixedTimeEquals(hashBytes, storedBytes);
        }
        catch (FormatException) { return false; }
    }

    public static bool NeedsRehash(string storedHash)
    {
        return !string.IsNullOrWhiteSpace(storedHash) && !storedHash.StartsWith("$pbkdf2-sha256$");
    }
}

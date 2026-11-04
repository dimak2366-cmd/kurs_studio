using System.Security.Cryptography;
using System.Text;

namespace Server.Security;

/// <summary>
/// Симметричное шифрование тела запроса алгоритмом AES-256-CBC.
/// Ключ и вектор инициализации берутся из appsettings.json (Security:AesKeyBase64, Security:AesIvBase64).
/// Используется для демонстрации шифрования передаваемых данных по HTTP
/// (в дополнение к штатному HTTPS, который настраивается на уровне Kestrel).
/// </summary>
public static class AesEncryptor
{
    public static string Encrypt(string plainText, string base64Key, string base64Iv)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var key = ValidateKey(base64Key, 32, "AesKeyBase64");
        var iv = ValidateKey(base64Iv, 16, "AesIvBase64");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public static string Decrypt(string cipherTextBase64, string base64Key, string base64Iv)
    {
        ArgumentNullException.ThrowIfNull(cipherTextBase64);
        var key = ValidateKey(base64Key, 32, "AesKeyBase64");
        var iv = ValidateKey(base64Iv, 16, "AesIvBase64");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] ValidateKey(string base64, int expectedLength, string settingName)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException($"Не задан параметр {settingName} в appsettings.json.");
        }

        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length != expectedLength)
        {
            throw new InvalidOperationException(
                $"Параметр {settingName} должен декодироваться в {expectedLength} байт (сейчас {bytes.Length}).");
        }

        return bytes;
    }
}

using System.Security.Cryptography;
using System.Text;
using Server.Infrastructure;

namespace Server.Security;

public interface IAesEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public sealed class AesEncryptor : IAesEncryptor
{
    private readonly byte[] _key;

    public AesEncryptor()
    {
        _key = ValidateKey(AppConfig.Instance.AesKeyBase64, 32, "AesKeyBase64");
    }

    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[16 + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
        Buffer.BlockCopy(cipherBytes, 0, result, 16, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherTextBase64)
    {
        ArgumentNullException.ThrowIfNull(cipherTextBase64);
        var data = Convert.FromBase64String(cipherTextBase64);

        if (data.Length <= 16)
            throw new CryptographicException("Сообщение короче IV — повреждено или зашифровано старым форматом.");

        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);

        var cipherBytes = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 16, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        try
        {
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Не удалось расшифровать: неверный ключ или повреждённые данные.");
        }
    }

    private static byte[] ValidateKey(string base64, int expectedLength, string settingName)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException($"Не задан параметр {settingName}.");

        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length != expectedLength)
            throw new InvalidOperationException($"Параметр {settingName} должен быть ровно {expectedLength} байт. Сейчас {bytes.Length}.");

        return bytes;
    }
}

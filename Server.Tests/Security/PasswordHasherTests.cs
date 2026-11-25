using Server.Security;
using Xunit;

namespace Server.Tests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesPbkdf2Format()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd!");

        Assert.StartsWith("$pbkdf2-sha256$100000$", hash);
        // Формат: $scheme$iterations$salt$hash → 5 сегментов
        Assert.Equal(5, hash.Split('$').Length);
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        var h1 = PasswordHasher.Hash("P@ssw0rd!");
        var h2 = PasswordHasher.Hash("P@ssw0rd!");

        Assert.NotEqual(h1, h2);           // случайная соль
        Assert.True(PasswordHasher.Verify("P@ssw0rd!", h1));
        Assert.True(PasswordHasher.Verify("P@ssw0rd!", h2));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd!");
        Assert.True(PasswordHasher.Verify("P@ssw0rd!", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd!");
        Assert.False(PasswordHasher.Verify("wrong", hash));
        Assert.False(PasswordHasher.Verify("P@ssw0rd",  hash)); // без воскл. знака
        Assert.False(PasswordHasher.Verify("P@ssw0rd! ", hash)); // с пробелом
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("$pbkdf2-sha256$broken")]
    [InlineData("$pbkdf2-sha256$100000$AAAA$BBBB")]
    [InlineData("")]
    public void Verify_GarbageStored_ReturnsFalse_DoesNotThrow(string stored)
    {
        // Мусор в БД не должен ронять эндпоинт — только 401.
        var result = PasswordHasher.Verify("any", stored);
        Assert.False(result);
    }

    [Fact]
    public void Verify_LegacySha256_ReturnsTrue_AndNeedsRehash()
    {
        // Старый формат: SHA256("legacy") в hex-нижнем регистре
        var legacyHash = "8bdb6e6e12d0be23a5b1e7d0c7b8e0a3f9d2e1c4d5a6b7c8d9e0f1a2b3c4d5e6";
        // Возьмём настоящий хеш SHA256("legacy"):
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("legacy"));
        var realLegacyHash = System.Convert.ToHexString(bytes).ToLowerInvariant();

        Assert.True(PasswordHasher.Verify("legacy", realLegacyHash));
        Assert.False(PasswordHasher.Verify("wrong",  realLegacyHash));
        Assert.True(PasswordHasher.NeedsRehash(realLegacyHash));
    }

    [Fact]
    public void NeedsRehash_CurrentFormat_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd!");
        Assert.False(PasswordHasher.NeedsRehash(hash));
    }

    [Fact]
    public void NeedsRehash_LowerIterations_ReturnsTrue()
    {
        // Формат с меньшим числом итераций → надо перехешировать
        var legacyPbkdf2 = "$pbkdf2-sha256$50000$c2FsdA==$aGFzaA==";
        Assert.True(PasswordHasher.NeedsRehash(legacyPbkdf2));
    }
}

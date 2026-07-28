using System.Security.Cryptography;
using TaskManagerAPI.Security;

namespace TaskManagerAPI.Tests;

public class PasswordHasherTests
{
    private const string Password = "Password123";

    [Fact]
    public void Hash_DoesNotContainPlainTextPassword()
    {
        var hash = PasswordHasher.Hash(Password);

        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void Hash_ProducesExpectedStructure()
    {
        var hash = PasswordHasher.Hash(Password);

        var segments = hash.Split('$');
        Assert.Equal(4, segments.Length);
        Assert.Equal("pbkdf2-sha256", segments[0]);

        Assert.True(int.TryParse(segments[1], out var iterations));
        Assert.Equal(600_000, iterations);

        var salt = Convert.FromBase64String(segments[2]);
        Assert.Equal(16, salt.Length);

        var derivedKey = Convert.FromBase64String(segments[3]);
        Assert.Equal(32, derivedKey.Length);
    }

    [Fact]
    public void HashAndVerify_WithCorrectPassword_ReturnsTrueAndNoRehash()
    {
        var hash = PasswordHasher.Hash(Password);

        var result = PasswordHasher.Verify(Password, hash, out var needsRehash);

        Assert.True(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash(Password);

        var result = PasswordHasher.Verify("WrongPassword", hash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_LegacyHash_WithCorrectPassword_ReturnsTrueAndNeedsRehash()
    {
        var legacyHash = TestAuthHelper.CreateLegacyHash(Password);

        var result = PasswordHasher.Verify(Password, legacyHash, out var needsRehash);

        Assert.True(result);
        Assert.True(needsRehash);
    }

    [Fact]
    public void Verify_LegacyHash_WithIncorrectPassword_ReturnsFalseAndNoRehash()
    {
        var legacyHash = TestAuthHelper.CreateLegacyHash(Password);

        var result = PasswordHasher.Verify("WrongPassword", legacyHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-recognized-format")]
    [InlineData("argon2$600000$c2FsdA==$aGFzaA==")]
    public void Verify_EmptyOrUnknownFormat_ReturnsFalse(string storedHash)
    {
        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Theory]
    [InlineData("pbkdf2-sha256$600000$not-valid-base64!$c2FsdA==")]
    [InlineData("pbkdf2-sha256$600000$c2FsdA==$not-valid-base64!")]
    [InlineData("not-valid-base64!.c2FsdA==")]
    public void Verify_InvalidBase64_ReturnsFalse(string storedHash)
    {
        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_WrongNumberOfSegments_ReturnsFalse()
    {
        var result = PasswordHasher.Verify(Password, "pbkdf2-sha256$600000$c2FsdA==", out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void Verify_InvalidIterationCount_ReturnsFalse(string iterations)
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var key = Convert.ToBase64String(new byte[32]);
        var storedHash = $"pbkdf2-sha256${iterations}${salt}${key}";

        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_IterationsAboveMaximum_ReturnsFalse()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var key = Convert.ToBase64String(new byte[32]);
        var storedHash = $"pbkdf2-sha256$1200001${salt}${key}";

        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_SaltWithIncorrectLength_ReturnsFalse()
    {
        var shortSalt = Convert.ToBase64String(new byte[8]);
        var validKey = Convert.ToBase64String(new byte[32]);
        var storedHash = $"pbkdf2-sha256$600000${shortSalt}${validKey}";

        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_DerivedKeyWithIncorrectLength_ReturnsFalse()
    {
        var validSalt = Convert.ToBase64String(new byte[16]);
        var shortKey = Convert.ToBase64String(new byte[16]);
        var storedHash = $"pbkdf2-sha256$600000${validSalt}${shortKey}";

        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void Verify_Pbkdf2HashWithIterationsBelowCurrent_ReturnsTrueAndNeedsRehash()
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(Password, salt, iterations, HashAlgorithmName.SHA256, 32);
        var storedHash = $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derivedKey)}";

        var result = PasswordHasher.Verify(Password, storedHash, out var needsRehash);

        Assert.True(result);
        Assert.True(needsRehash);
    }
}

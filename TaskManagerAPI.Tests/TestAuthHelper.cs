using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

internal static class TestAuthHelper
{
    private const string Password = "Password123";

    public static string UniqueUsername(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static string CreateLegacyHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var payload = new byte[salt.Length + passwordBytes.Length];

        Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, payload, salt.Length, passwordBytes.Length);

        var hash = SHA256.HashData(payload);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string username)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = username,
            Password = Password
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = username,
            Password = Password
        });
        loginResponse.EnsureSuccessStatusCode();

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse!.Token;
    }
}

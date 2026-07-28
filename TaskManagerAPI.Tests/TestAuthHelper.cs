using System.Net.Http.Json;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

internal static class TestAuthHelper
{
    private const string Password = "Password123";

    public static string UniqueUsername(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

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

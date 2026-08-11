using System.Net;
using System.Net.Http.Json;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_WithDifferentCasingAndWhitespace_SucceedsForSameNormalizedUsername()
    {
        var client = _factory.CreateClient();
        var rawUsername = $"  UsEr-{Guid.NewGuid():N}  ";
        const string password = "Password123";

        var registerResponse = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = rawUsername,
            Password = password
        });
        registerResponse.EnsureSuccessStatusCode();

        var differentCasingUsername = rawUsername.Trim().ToUpperInvariant();
        var loginResponse = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = differentCasingUsername,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
    }
}

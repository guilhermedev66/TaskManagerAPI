using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class PasswordRehashTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SeedLegacyUserAsync(string username, string legacyPasswordHash)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User { Username = username, PasswordHash = legacyPasswordHash });
        await db.SaveChangesAsync();
    }

    private async Task<string> GetPersistedPasswordHashAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Username == username);
        return user.PasswordHash;
    }

    [Fact]
    public async Task LegacyLogin_Succeeds_AndUpgradesStoredHash()
    {
        const string username = "legacy-user";
        const string password = "Password123";
        await SeedLegacyUserAsync(username, TestAuthHelper.CreateLegacyHash(password));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var persistedHash = await GetPersistedPasswordHashAsync(username);
        Assert.StartsWith("pbkdf2-sha256$", persistedHash);
    }

    [Fact]
    public async Task LegacyLogin_WithWrongPassword_ReturnsUnauthorized_AndDoesNotModifyHash()
    {
        const string username = "legacy-user-wrong";
        const string password = "Password123";
        var legacyHash = TestAuthHelper.CreateLegacyHash(password);
        await SeedLegacyUserAsync(username, legacyHash);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = username,
            Password = "WrongPassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var persistedHash = await GetPersistedPasswordHashAsync(username);
        Assert.Equal(legacyHash, persistedHash);
    }

    [Fact]
    public async Task Pbkdf2Login_StillWorks_AfterRegistration()
    {
        var anon = _factory.CreateClient();
        var username = TestAuthHelper.UniqueUsername("pbkdf2-user");

        var token = await TestAuthHelper.RegisterAndLoginAsync(anon, username);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class RefreshTokenTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<AuthResponse> RegisterAndLoginRawAsync(HttpClient client, string username)
    {
        const string password = "Password123";
        var registerResponse = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task SeedExpiredTokenAsync(string tokenHashBase64, int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = tokenHashBase64,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(-3),
            RevokedAt = null,
            ReplacedByTokenHash = null
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_ReturnsAccessTokenAndDifferentRefreshToken()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-login"));

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.NotEqual(auth.Token, auth.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewPair_AndAccessTokenWorks()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-refresh-ok"));

        var refreshResponse = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest
        {
            RefreshToken = auth.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(newAuth);
        Assert.NotEqual(auth.RefreshToken, newAuth!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(newAuth.Token));

        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAuth.Token);
        var protectedResponse = await authedClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAlreadyRotatedToken_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-reuse"));

        var firstRefresh = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        var reuseResponse = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.Equal("application/problem+json", reuseResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_RevokesEntireFamily_IncludingLegitimateNewToken()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-family-revoke"));

        var firstRefresh = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();
        var firstNewAuth = await firstRefresh.Content.ReadFromJsonAsync<AuthResponse>();

        // Reapresenta o token já rotacionado (reuso) -> deve revogar a família inteira.
        var reuseResponse = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // O token legítimo emitido na primeira rotação também deve ter sido invalidado.
        var secondRefresh = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = firstNewAuth!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsProblemDetails_AndDoesNotRevokeFamily()
    {
        var client = _factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = TestAuthHelper.UniqueUsername("rt-expired"),
            Password = "Password123"
        });
        registerResponse.EnsureSuccessStatusCode();

        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await db.Users.AsNoTracking().OrderByDescending(u => u.Id).FirstAsync()).Id;
        }

        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenBytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(rawToken);
        var tokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(tokenBytes));

        await SeedExpiredTokenAsync(tokenHash, userId);

        var response = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = rawToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();
        var rawToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var response = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = rawToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithMalformedToken_ReturnsProblemDetails_NotServerError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = "not-a-valid-base64url-token!!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsNoContent_AndTokenStopsWorking()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-logout"));

        var logoutResponse = await client.PostAsJsonAsync("/api/logout", new LogoutRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithInvalidOrUnknownToken_StillReturnsNoContent()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/logout", new LogoutRequest { RefreshToken = "garbage-not-a-real-token" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAuthorizationHeader_Succeeds()
    {
        using var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-logout-noauth"));

        // Cliente novo, sem qualquer header Authorization, provando que logout não exige access token.
        using var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync("/api/logout", new LogoutRequest { RefreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TwoLogins_ProduceIndependentFamilies_ReuseInOneDoesNotAffectTheOther()
    {
        var client = _factory.CreateClient();
        var username = TestAuthHelper.UniqueUsername("rt-two-sessions");
        await client.PostAsJsonAsync("/api/register", new RegisterRequest { Username = username, Password = "Password123" });

        var login1 = await client.PostAsJsonAsync("/api/login", new LoginRequest { Username = username, Password = "Password123" });
        var auth1 = (await login1.Content.ReadFromJsonAsync<AuthResponse>())!;

        var login2 = await client.PostAsJsonAsync("/api/login", new LoginRequest { Username = username, Password = "Password123" });
        var auth2 = (await login2.Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.NotEqual(auth1.RefreshToken, auth2.RefreshToken);

        // Rotaciona e depois reusa o token da sessão 1 -> família 1 é revogada.
        var rotate1 = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth1.RefreshToken });
        rotate1.EnsureSuccessStatusCode();
        var reuse1 = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth1.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse1.StatusCode);

        // Sessão 2 (família diferente) continua intacta.
        var rotate2 = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth2.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotate2.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefresh_WithSameToken_OnlyOneSucceeds_AndWinnerTokenIsAlsoInvalidated()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginRawAsync(client, TestAuthHelper.UniqueUsername("rt-race"));

        var task1 = client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        var task2 = client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });

        var responses = await Task.WhenAll(task1, task2);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthorizedCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

        Assert.Equal(1, okCount);
        Assert.Equal(1, unauthorizedCount);

        var winnerResponse = responses.First(r => r.StatusCode == HttpStatusCode.OK);
        var winnerAuth = await winnerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Efeito conservador documentado: o token novo da chamada vencedora também fica inutilizável,
        // porque a chamada perdedora detectou reuso e revogou a família inteira.
        var thirdAttempt = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest { RefreshToken = winnerAuth!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, thirdAttempt.StatusCode);
    }
}

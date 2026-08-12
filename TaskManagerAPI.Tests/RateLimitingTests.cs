using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class RateLimitingTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_AfterLimit_ReturnsProblemDetails()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/login", new LoginRequest
            {
                Username = TestAuthHelper.UniqueUsername("limited-login"),
                Password = "WrongPassword123"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = TestAuthHelper.UniqueUsername("limited-login"),
            Password = "WrongPassword123"
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.Status);
    }

    [Fact]
    public async Task Refresh_UsesIndependentLimit()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest
            {
                RefreshToken = "malformed-token"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/refresh", new RefreshRequest
        {
            RefreshToken = "malformed-token"
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }
}

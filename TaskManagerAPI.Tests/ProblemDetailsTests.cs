using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class ProblemDetailsTests : IDisposable
{
    private const string ProblemJsonMediaType = "application/problem+json";

    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();
        var username = TestAuthHelper.UniqueUsername("dup-user");

        var first = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = username,
            Password = "Password123"
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = username,
            Password = "Password123"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ProblemJsonMediaType, second.Content.Headers.ContentType?.MediaType);

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal("Usuário já existe.", problem.Detail);
    }

    [Fact]
    public async Task Register_InvalidPayload_ReturnsValidationProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/register", new RegisterRequest
        {
            Username = "ab",
            Password = "123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.True(problem.Errors.ContainsKey(nameof(RegisterRequest.Username)));
        Assert.True(problem.Errors.ContainsKey(nameof(RegisterRequest.Password)));
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/login", new LoginRequest
        {
            Username = TestAuthHelper.UniqueUsername("no-such-user"),
            Password = "WrongPassword123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem!.Status);
        Assert.Equal("Credenciais inválidas.", problem.Detail);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem!.Status);
    }

    [Fact]
    public async Task GetTaskById_NonExistentTask_ReturnsProblemDetails()
    {
        var anonymousClient = _factory.CreateClient();
        var token = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("notfound-user"));
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/tasks/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
    }

    [Fact]
    public async Task CreateTask_DueDateInPast_ReturnsProblemDetails()
    {
        var anonymousClient = _factory.CreateClient();
        var token = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("pastdue-user"));
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            Title = "Tarefa com prazo vencido",
            DueDate = DateTime.UtcNow.AddDays(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("DueDate não pode estar no passado.", problem.Detail);
    }

    [Fact]
    public async Task CreateTask_InvalidPayload_ReturnsValidationProblemDetails()
    {
        var anonymousClient = _factory.CreateClient();
        var token = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("badtask-user"));
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            Title = string.Empty
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey(nameof(CreateTaskRequest.Title)));
    }

    [Fact]
    public async Task SearchTasks_MissingTitle_ReturnsProblemDetails()
    {
        var anonymousClient = _factory.CreateClient();
        var token = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("search-user"));
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/tasks/search?title=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
    }
}

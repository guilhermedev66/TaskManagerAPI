using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class TaskOwnershipTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(HttpClient ClientA, HttpClient ClientB, int TaskId)> CreateTaskOwnedByUserAAsync()
    {
        var anonymousClient = _factory.CreateClient();
        var tokenA = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("user-a"));
        var tokenB = await TestAuthHelper.RegisterAndLoginAsync(anonymousClient, TestAuthHelper.UniqueUsername("user-b"));

        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        var createResponse = await clientA.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            Title = "Task from user A"
        });
        createResponse.EnsureSuccessStatusCode();
        var task = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        return (clientA, clientB, task!.Id);
    }

    [Fact]
    public async Task UserB_DoesNotSeeUserATaskInList()
    {
        var (_, clientB, _) = await CreateTaskOwnedByUserAAsync();

        var listResponse = await clientB.GetAsync("/api/tasks");
        listResponse.EnsureSuccessStatusCode();
        var tasks = await listResponse.Content.ReadFromJsonAsync<List<TaskItem>>();

        Assert.Empty(tasks!);
    }

    [Fact]
    public async Task UserB_GetsNotFound_ForUserATaskById()
    {
        var (_, clientB, taskId) = await CreateTaskOwnedByUserAAsync();

        var response = await clientB.GetAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UserB_GetsNotFound_OnUpdate_AndTaskIsUnchanged()
    {
        var (clientA, clientB, taskId) = await CreateTaskOwnedByUserAAsync();

        var updateResponse = await clientB.PutAsJsonAsync($"/api/tasks/{taskId}", new UpdateTaskRequest
        {
            Title = "Hijacked by user B",
            IsCompleted = true
        });

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var getResponse = await clientA.GetAsync($"/api/tasks/{taskId}");
        getResponse.EnsureSuccessStatusCode();
        var task = await getResponse.Content.ReadFromJsonAsync<TaskItem>();

        Assert.Equal("Task from user A", task!.Title);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public async Task UserB_GetsNotFound_OnDelete_AndTaskStillExists()
    {
        var (clientA, clientB, taskId) = await CreateTaskOwnedByUserAAsync();

        var deleteResponse = await clientB.DeleteAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var getResponse = await clientA.GetAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task UserA_CanReadUpdateAndDeleteOwnTask()
    {
        var (clientA, _, taskId) = await CreateTaskOwnedByUserAAsync();

        var getResponse = await clientA.GetAsync($"/api/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await clientA.PutAsJsonAsync($"/api/tasks/{taskId}", new UpdateTaskRequest
        {
            Title = "Updated by owner",
            IsCompleted = true
        });
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var deleteResponse = await clientA.DeleteAsync($"/api/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDeleteResponse = await clientA.GetAsync($"/api/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

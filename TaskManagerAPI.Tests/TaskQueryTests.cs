using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Tests;

public class TaskQueryTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> CreateClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await TestAuthHelper.RegisterAndLoginAsync(
            client,
            TestAuthHelper.UniqueUsername("task-query"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task CreateTaskAsync(
        HttpClient client,
        string title,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            Title = title,
            Priority = priority,
            DueDate = dueDate
        });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetTasks_ReturnsPagedMetadata()
    {
        using var client = await CreateClientAsync();
        await CreateTaskAsync(client, "Task 1");
        await CreateTaskAsync(client, "Task 2");
        await CreateTaskAsync(client, "Task 3");

        var response = await client.GetFromJsonAsync<PagedResponse<TaskItem>>(
            "/api/tasks?page=2&pageSize=2&sortBy=title&sortDirection=asc");

        Assert.NotNull(response);
        Assert.Single(response.Items);
        Assert.Equal("Task 3", response.Items[0].Title);
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(3, response.TotalItems);
        Assert.Equal(2, response.TotalPages);
    }

    [Fact]
    public async Task GetTasks_FiltersByStatusAndTitle_AndOrdersByPriority()
    {
        using var client = await CreateClientAsync();
        await CreateTaskAsync(client, "Study API", TaskPriority.Low);
        await CreateTaskAsync(client, "Study tests", TaskPriority.High);
        await CreateTaskAsync(client, "Unrelated", TaskPriority.High);

        var response = await client.GetFromJsonAsync<PagedResponse<TaskItem>>(
            "/api/tasks?status=pending&title=study&sortBy=priority&sortDirection=desc");

        Assert.NotNull(response);
        Assert.Equal(2, response.TotalItems);
        Assert.Collection(
            response.Items,
            first => Assert.Equal("Study tests", first.Title),
            second => Assert.Equal("Study API", second.Title));
    }

    [Theory]
    [InlineData("asc", "Near deadline", "Far deadline", "No deadline")]
    [InlineData("desc", "Far deadline", "Near deadline", "No deadline")]
    public async Task GetTasks_OrdersByDueDate_WithNullDueDatesLast(
        string direction,
        string firstTitle,
        string secondTitle,
        string thirdTitle)
    {
        using var client = await CreateClientAsync();
        await CreateTaskAsync(client, "No deadline");
        await CreateTaskAsync(client, "Far deadline", dueDate: DateTime.UtcNow.AddDays(10));
        await CreateTaskAsync(client, "Near deadline", dueDate: DateTime.UtcNow.AddDays(2));

        var response = await client.GetFromJsonAsync<PagedResponse<TaskItem>>(
            $"/api/tasks?sortBy=dueDate&sortDirection={direction}");

        Assert.NotNull(response);
        Assert.Equal(3, response.TotalItems);
        Assert.Equal(1, response.TotalPages);
        Assert.Collection(
            response.Items,
            first => Assert.Equal(firstTitle, first.Title),
            second => Assert.Equal(secondTitle, second.Title),
            third => Assert.Equal(thirdTitle, third.Title));
    }

    [Theory]
    [InlineData("/api/tasks?page=0")]
    [InlineData("/api/tasks?pageSize=101")]
    [InlineData("/api/tasks?status=invalid")]
    [InlineData("/api/tasks?sortBy=invalid")]
    public async Task GetTasks_WithInvalidQuery_ReturnsValidationProblem(string path)
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

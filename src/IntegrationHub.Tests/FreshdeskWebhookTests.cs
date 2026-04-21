using System.Net;
using Xunit;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationHub.Tests;

public class FreshdeskWebhookTests(IntegrationHubFactory factory)
    : IClassFixture<IntegrationHubFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly List<string> _asanaTasksToDelete = [];

    private static readonly HttpClient AsanaHttp = new();

    static FreshdeskWebhookTests()
    {
        AsanaHttp.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", "2/1214144897180059/1214147882588535:67facc4a02a7286c13fc62dc2b1b8c2a");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var gid in _asanaTasksToDelete)
            await AsanaHttp.DeleteAsync($"https://app.asana.com/api/1.0/tasks/{gid}");
    }

    [Fact]
    public async Task MissingTicket_Returns_BadRequest()
    {
        var payload = new { ticket = (object?)null };

        var response = await _client.PostAsJsonAsync("/webhooks/freshdesk/ticket", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidTicket_Creates_AsanaTask_And_Returns_TaskId()
    {
        var payload = new
        {
            ticket = new
            {
                id = 900001,
                subject = "[TEST] FD Webhook → Asana",
                description = "Integration test ticket - safe to delete"
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/freshdesk/ticket", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = body.GetProperty("asanaTaskId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(taskId));

        _asanaTasksToDelete.Add(taskId!);

        // Verify the task actually exists in Asana
        var asanaResponse = await AsanaHttp.GetAsync($"https://app.asana.com/api/1.0/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, asanaResponse.StatusCode);
        var task = await asanaResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("[TEST] FD Webhook → Asana", task.GetProperty("data").GetProperty("name").GetString());
    }
}

using System.Net;
using Xunit;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationHub.Api.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Tests;

/// <summary>
/// Full round-trip tests: Freshdesk ↔ Asana via webhook endpoints.
/// Each test creates real resources and cleans them up in DisposeAsync.
/// </summary>
public class EndToEndSyncTests(IntegrationHubFactory factory)
    : IClassFixture<IntegrationHubFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly IServiceProvider _services = factory.Services;
    private readonly List<string> _asanaTasksToDelete = [];
    private readonly List<long> _freshdeskTicketsToDelete = [];

    private static readonly HttpClient AsanaHttp = BuildAsanaClient();
    private static readonly HttpClient FreshdeskHttp = BuildFreshdeskClient();

    private static HttpClient BuildAsanaClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", "2/1214144897180059/1214147882588535:67facc4a02a7286c13fc62dc2b1b8c2a");
        return c;
    }

    private static HttpClient BuildFreshdeskClient()
    {
        var c = new HttpClient();
        var creds = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("eO9TbeseuWkOQPk2meC9:X"));
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        return c;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var gid in _asanaTasksToDelete)
            await AsanaHttp.DeleteAsync($"https://app.asana.com/api/1.0/tasks/{gid}");

        foreach (var id in _freshdeskTicketsToDelete)
            await FreshdeskHttp.DeleteAsync($"https://pbwebstudio-help.freshdesk.com/api/v2/tickets/{id}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Freshdesk → Asana
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FreshdeskTicket_Webhook_Creates_AsanaTask()
    {
        var payload = new
        {
            ticket = new
            {
                id = 900010,
                subject = "[TEST] E2E: FD→Asana task creation",
                description = "Created by integration test - safe to delete"
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/freshdesk/ticket", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = body.GetProperty("asanaTaskId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(taskId));
        _asanaTasksToDelete.Add(taskId!);

        // Confirm Asana task exists with correct name
        var asanaResp = await AsanaHttp.GetAsync($"https://app.asana.com/api/1.0/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, asanaResp.StatusCode);
        var task = await asanaResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("[TEST] E2E: FD→Asana task creation",
            task.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task FreshdeskTicket_Webhook_Saves_Mapping()
    {
        var payload = new
        {
            ticket = new
            {
                id = 900011,
                subject = "[TEST] E2E: mapping persistence",
                description = "Integration test - safe to delete"
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/freshdesk/ticket", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var taskId = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("asanaTaskId").GetString()!;
        _asanaTasksToDelete.Add(taskId);

        // Confirm mapping persisted
        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMappingRepository>();
        var mapping = repo.GetByFreshdeskId(900011);
        Assert.NotNull(mapping);
        Assert.Equal(taskId, mapping.AsanaTaskId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Asana → Freshdesk
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AsanaTask_Webhook_Creates_FreshdeskTicket()
    {
        // Create a real Asana task to trigger with
        var createResp = await AsanaHttp.PostAsJsonAsync(
            "https://app.asana.com/api/1.0/tasks",
            new
            {
                data = new
                {
                    name = "[TEST] E2E: Asana→FD ticket creation",
                    notes = "Created by integration test - safe to delete",
                    workspace = "1213509069446553"
                }
            });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var taskGid = created.GetProperty("data").GetProperty("gid").GetString()!;
        _asanaTasksToDelete.Add(taskGid);

        // Trigger the webhook
        var webhookPayload = new
        {
            events = new[]
            {
                new
                {
                    resource = new { gid = taskGid, resource_type = "task" },
                    action = "changed",
                    change = new { field = "name" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", webhookPayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Confirm Freshdesk ticket was created via mapping
        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMappingRepository>();
        var mapping = repo.GetByAsanaTaskId(taskGid);
        Assert.NotNull(mapping);
        _freshdeskTicketsToDelete.Add(mapping.FreshdeskTicketId);

        // Confirm ticket exists in Freshdesk
        var fdResp = await FreshdeskHttp.GetAsync(
            $"https://pbwebstudio-help.freshdesk.com/api/v2/tickets/{mapping.FreshdeskTicketId}");
        Assert.Equal(HttpStatusCode.OK, fdResp.StatusCode);
        var ticket = await fdResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("[TEST] E2E: Asana→FD ticket creation",
            ticket.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task AsanaTask_Webhook_Updates_ExistingFreshdeskTicket()
    {
        // Create Asana task
        var createResp = await AsanaHttp.PostAsJsonAsync(
            "https://app.asana.com/api/1.0/tasks",
            new
            {
                data = new
                {
                    name = "[TEST] E2E: update sync - original",
                    notes = "Original notes",
                    workspace = "1213509069446553"
                }
            });
        var taskGid = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("gid").GetString()!;
        _asanaTasksToDelete.Add(taskGid);

        var webhookPayload = new
        {
            events = new[]
            {
                new
                {
                    resource = new { gid = taskGid, resource_type = "task" },
                    action = "changed",
                    change = new { field = "name" }
                }
            }
        };

        // First sync — creates Freshdesk ticket
        var firstSync = await _client.PostAsJsonAsync("/webhooks/asana", webhookPayload);
        Assert.Equal(HttpStatusCode.OK, firstSync.StatusCode);

        using var scope1 = _services.CreateScope();
        var mapping = scope1.ServiceProvider
            .GetRequiredService<IMappingRepository>()
            .GetByAsanaTaskId(taskGid);
        Assert.NotNull(mapping);
        _freshdeskTicketsToDelete.Add(mapping.FreshdeskTicketId);

        // Rename the Asana task
        await AsanaHttp.PutAsJsonAsync(
            $"https://app.asana.com/api/1.0/tasks/{taskGid}",
            new { data = new { name = "[TEST] E2E: update sync - renamed" } });

        // Second sync — should update the same Freshdesk ticket
        var response = await _client.PostAsJsonAsync("/webhooks/asana", webhookPayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fdResp = await FreshdeskHttp.GetAsync(
            $"https://pbwebstudio-help.freshdesk.com/api/v2/tickets/{mapping.FreshdeskTicketId}");
        var ticket = await fdResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("[TEST] E2E: update sync - renamed",
            ticket.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task CompletedAsanaTask_Resolves_FreshdeskTicket()
    {
        // Create and complete an Asana task
        var createResp = await AsanaHttp.PostAsJsonAsync(
            "https://app.asana.com/api/1.0/tasks",
            new
            {
                data = new
                {
                    name = "[TEST] E2E: task completion → ticket resolved",
                    notes = "Should resolve the Freshdesk ticket",
                    workspace = "1213509069446553",
                    completed = true
                }
            });
        var taskGid = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("gid").GetString()!;
        _asanaTasksToDelete.Add(taskGid);

        var webhookPayload = new
        {
            events = new[]
            {
                new
                {
                    resource = new { gid = taskGid, resource_type = "task" },
                    action = "changed",
                    change = new { field = "completed" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", webhookPayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _services.CreateScope();
        var mapping = scope.ServiceProvider
            .GetRequiredService<IMappingRepository>()
            .GetByAsanaTaskId(taskGid)!;
        Assert.NotNull(mapping);
        _freshdeskTicketsToDelete.Add(mapping.FreshdeskTicketId);

        // Freshdesk status 4 = Resolved
        var fdResp = await FreshdeskHttp.GetAsync(
            $"https://pbwebstudio-help.freshdesk.com/api/v2/tickets/{mapping.FreshdeskTicketId}");
        var ticket = await fdResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, ticket.GetProperty("status").GetInt32());
    }
}

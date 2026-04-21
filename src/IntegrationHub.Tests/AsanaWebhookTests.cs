using System.Net;
using Xunit;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationHub.Tests;

public class AsanaWebhookTests(IntegrationHubFactory factory)
    : IClassFixture<IntegrationHubFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Challenge_Returns_ChallengeValue()
    {
        var payload = new { challenge = "handshake-token-abc123" };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("handshake-token-abc123", body.GetProperty("challenge").GetString());
    }

    [Fact]
    public async Task NullEvents_Returns_OK()
    {
        var payload = new { events = (object?)null };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmptyEventsList_Returns_OK()
    {
        var payload = new { events = Array.Empty<object>() };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonTaskEvent_Returns_OK_Without_Syncing()
    {
        var payload = new
        {
            events = new[]
            {
                new
                {
                    resource = new { gid = "999999", resource_type = "project" },
                    action = "changed",
                    change = new { field = "name" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/webhooks/asana", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

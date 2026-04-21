using System.Net;
using Xunit;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationHub.Tests;

public class IntegrationsControllerTests(IntegrationHubFactory factory)
    : IClassFixture<IntegrationHubFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetIntegrations_Returns_ExpectedList()
    {
        var response = await _client.GetAsync("/api/integrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var integrations = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(integrations);
        Assert.Contains("Asana", integrations);
        Assert.Contains("Freshdesk", integrations);
        Assert.Contains("Database", integrations);
    }

    [Fact]
    public async Task GetSettings_Returns_SettingsObject()
    {
        var response = await _client.GetAsync("/api/integrations/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("name", out var name) || json.TryGetProperty("Name", out name));
        Assert.False(string.IsNullOrWhiteSpace(name.GetString()));
    }
}

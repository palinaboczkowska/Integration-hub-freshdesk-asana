using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationHub.Api.Integrations.Asana;

public class AsanaClient
{
    private readonly HttpClient _httpClient;

    public AsanaClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        var baseUrl = configuration["Asana:BaseUrl"] ?? "https://app.asana.com/api/1.0";
        var token = configuration["Asana:Token"] ?? "YOUR_ASANA_TOKEN";

        // Trailing slash is required: without it HttpClient replaces the last path segment
        // when combining the base ("api/1.0") with a relative path ("tasks").
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // Paths must NOT start with "/" to correctly resolve against BaseAddress (/api/1.0).
    public Task<HttpResponseMessage> CreateTask(object taskPayload, CancellationToken cancellationToken = default) =>
        _httpClient.PostAsJsonAsync("tasks", taskPayload, cancellationToken);

    public Task<HttpResponseMessage> UpdateTask(string taskId, object taskPayload, CancellationToken cancellationToken = default) =>
        _httpClient.PutAsJsonAsync($"tasks/{taskId}", taskPayload, cancellationToken);

    public Task<HttpResponseMessage> AddComment(string taskId, object commentPayload, CancellationToken cancellationToken = default) =>
        _httpClient.PostAsJsonAsync($"tasks/{taskId}/stories", commentPayload, cancellationToken);

    public Task<HttpResponseMessage> GetTask(string taskId, CancellationToken cancellationToken = default) =>
        _httpClient.GetAsync($"tasks/{taskId}", cancellationToken);
}

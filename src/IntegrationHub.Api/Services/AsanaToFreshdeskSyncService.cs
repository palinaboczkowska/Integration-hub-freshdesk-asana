using System.Net.Http.Json;
using IntegrationHub.Api.Integrations.Asana;
using IntegrationHub.Api.Integrations.Freshdesk;
using IntegrationHub.Api.Models;
using IntegrationHub.Api.Repositories;

namespace IntegrationHub.Api.Services;

public class AsanaToFreshdeskSyncService(
    AsanaClient asanaClient,
    FreshdeskClient freshdeskClient,
    IMappingRepository mappingRepository,
    ILogger<AsanaToFreshdeskSyncService> logger)
    : IAsanaToFreshdeskSyncService
{
    private readonly AsanaClient _asanaClient = asanaClient;
    private readonly FreshdeskClient _freshdeskClient = freshdeskClient;
    private readonly IMappingRepository _mappingRepository = mappingRepository;
    private readonly ILogger<AsanaToFreshdeskSyncService> _logger = logger;

    public async Task SyncTaskToFreshdeskAsync(string asanaTaskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(asanaTaskId))
        {
            throw new ArgumentException("Asana task id must be provided.", nameof(asanaTaskId));
        }

        // Get Asana task details
        var asanaResponse = await _asanaClient.GetTask(asanaTaskId, cancellationToken);
        asanaResponse.EnsureSuccessStatusCode();

        var asanaTask = await asanaResponse.Content.ReadFromJsonAsync<AsanaTaskDto>(cancellationToken: cancellationToken);
        if (asanaTask?.Data == null)
        {
            throw new InvalidOperationException($"Unable to deserialize Asana task {asanaTaskId}");
        }

        var existingMapping = _mappingRepository.GetByAsanaTaskId(asanaTaskId);

        long freshdeskTicketId;

        if (existingMapping == null)
        {
            // Create new Freshdesk ticket
            var createPayload = new
            {
                subject = asanaTask.Data.Name,
                description = asanaTask.Data.Notes
            };

            var fdResponse = await _freshdeskClient.CreateTicket(createPayload, cancellationToken);
            fdResponse.EnsureSuccessStatusCode();

            var createdTicket = await fdResponse.Content.ReadFromJsonAsync<FreshdeskTicketCreatedDto>(cancellationToken: cancellationToken);
            if (createdTicket == null)
            {
                throw new InvalidOperationException("Unable to deserialize created Freshdesk ticket response.");
            }

            freshdeskTicketId = createdTicket.Id;

            var mapping = new TicketSyncMapping
            {
                FreshdeskTicketId = freshdeskTicketId,
                AsanaTaskId = asanaTaskId,
                SyncedAtUtc = DateTime.UtcNow
            };

            await _mappingRepository.SaveAsync(mapping, cancellationToken);

            _logger.LogInformation("Created Freshdesk ticket {TicketId} from Asana task {TaskId}", freshdeskTicketId, asanaTaskId);
        }
        else
        {
            // Update existing Freshdesk ticket
            freshdeskTicketId = existingMapping.FreshdeskTicketId;

            var updatePayload = new
            {
                subject = asanaTask.Data.Name,
                description = asanaTask.Data.Notes
            };

            var fdResponse = await _freshdeskClient.UpdateTicket(freshdeskTicketId, updatePayload, cancellationToken);
            fdResponse.EnsureSuccessStatusCode();

            _logger.LogInformation("Updated Freshdesk ticket {TicketId} from Asana task {TaskId}", freshdeskTicketId, asanaTaskId);
        }

        // Close ticket if task is completed
        if (asanaTask.Data.Completed)
        {
            var closePayload = new
            {
                status = 4 // Resolved
            };

            var closeResponse = await _freshdeskClient.UpdateTicket(freshdeskTicketId, closePayload, cancellationToken);
            closeResponse.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Closed Freshdesk ticket {TicketId} because Asana task {TaskId} is completed",
                freshdeskTicketId,
                asanaTaskId);
        }


    }

    // Minimal Asana task DTO for reverse sync
    private sealed class AsanaTaskDto
    {
        public AsanaTaskData? Data { get; set; }
    }

    private sealed class AsanaTaskData
    {
        public string Gid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool Completed { get; set; }

    }

    private sealed class FreshdeskTicketCreatedDto
    {
        public long Id { get; set; }
    }
}


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
    IConfiguration configuration,
    ILogger<AsanaToFreshdeskSyncService> logger)
    : IAsanaToFreshdeskSyncService
{
    private const int FreshdeskOpenStatus = 2;
    private const int FreshdeskResolvedStatus = 4;
    private const int FreshdeskDefaultPriority = 1; // Low

    public async Task SyncTaskToFreshdeskAsync(string asanaTaskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(asanaTaskId))
            throw new ArgumentException("Asana task id must be provided.", nameof(asanaTaskId));

        var asanaResponse = await asanaClient.GetTask(asanaTaskId, cancellationToken);
        asanaResponse.EnsureSuccessStatusCode();

        var asanaTask = await asanaResponse.Content.ReadFromJsonAsync<AsanaTaskDto>(cancellationToken: cancellationToken);
        if (asanaTask?.Data == null)
            throw new InvalidOperationException($"Unable to deserialize Asana task {asanaTaskId}");

        var systemEmail = configuration["Freshdesk:SystemEmail"]
            ?? throw new InvalidOperationException("Freshdesk:SystemEmail is not configured.");

        var ticketFields = new
        {
            subject = asanaTask.Data.Name,
            description = asanaTask.Data.Notes,
            email = systemEmail,
            status = FreshdeskOpenStatus,
            priority = FreshdeskDefaultPriority
        };
        var existingMapping = mappingRepository.GetByAsanaTaskId(asanaTaskId);
        long freshdeskTicketId;

        if (existingMapping == null)
        {
            var fdResponse = await freshdeskClient.CreateTicket(ticketFields, cancellationToken);
            if (!fdResponse.IsSuccessStatusCode)
            {
                var errorBody = await fdResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Failed to create Freshdesk ticket from Asana task {TaskId}. Status: {Status}, Body: {Body}",
                    asanaTaskId, (int)fdResponse.StatusCode, errorBody);
                fdResponse.EnsureSuccessStatusCode();
            }

            var createdTicket = await fdResponse.Content.ReadFromJsonAsync<FreshdeskTicketCreatedDto>(cancellationToken: cancellationToken);
            if (createdTicket == null)
                throw new InvalidOperationException("Unable to deserialize created Freshdesk ticket response.");

            freshdeskTicketId = createdTicket.Id;

            await mappingRepository.SaveAsync(new TicketSyncMapping
            {
                FreshdeskTicketId = freshdeskTicketId,
                AsanaTaskId = asanaTaskId,
                SyncedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            logger.LogInformation("Created Freshdesk ticket {TicketId} from Asana task {TaskId}", freshdeskTicketId, asanaTaskId);
        }
        else
        {
            freshdeskTicketId = existingMapping.FreshdeskTicketId;

            (await freshdeskClient.UpdateTicket(freshdeskTicketId, ticketFields, cancellationToken)).EnsureSuccessStatusCode();

            logger.LogInformation("Updated Freshdesk ticket {TicketId} from Asana task {TaskId}", freshdeskTicketId, asanaTaskId);
        }

        if (asanaTask.Data.Completed)
        {
            (await freshdeskClient.UpdateTicket(freshdeskTicketId, new { status = FreshdeskResolvedStatus }, cancellationToken)).EnsureSuccessStatusCode();

            logger.LogInformation(
                "Closed Freshdesk ticket {TicketId} because Asana task {TaskId} is completed",
                freshdeskTicketId, asanaTaskId);
        }
    }

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

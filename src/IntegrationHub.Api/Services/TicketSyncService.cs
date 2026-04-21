using System.Net.Http.Json;
using IntegrationHub.Api.Integrations.Asana;
using IntegrationHub.Api.Integrations.Freshdesk;
using IntegrationHub.Api.Models;
using IntegrationHub.Api.Repositories;

namespace IntegrationHub.Api.Services;

public class TicketSyncService(
    FreshdeskClient freshdeskClient,
    AsanaClient asanaClient,
    IMappingRepository mappingRepository,
    IConfiguration configuration,
    ILogger<TicketSyncService> logger)
    : ITicketSyncService
{
    public async Task<string> SyncFromFreshdeskTicketAsync(FreshdeskTicket ticket, CancellationToken cancellationToken = default)
    {
        var workspaceId = configuration["Asana:WorkspaceId"] ?? throw new InvalidOperationException("Asana:WorkspaceId is not configured.");
        var projectId = configuration["Asana:ProjectId"] ?? throw new InvalidOperationException("Asana:ProjectId is not configured.");

        var asanaPayload = new
        {
            data = new
            {
                name = ticket.Subject,
                notes = ticket.Description,
                workspace = workspaceId,
                projects = new[] { projectId }
            }
        };

        var response = await asanaClient.CreateTask(asanaPayload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Failed to create Asana task for Freshdesk ticket {TicketId}. StatusCode: {StatusCode}, Body: {Body}",
                ticket.Id, (int)response.StatusCode, errorBody);

            throw new InvalidOperationException(
                $"Asana task creation failed with status {(int)response.StatusCode}: {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<AsanaTaskCreatedResponse>(cancellationToken: cancellationToken);
        if (created?.Data == null || string.IsNullOrWhiteSpace(created.Data.Gid))
        {
            logger.LogWarning("Asana task creation succeeded but response body was unexpected for Freshdesk ticket {TicketId}", ticket.Id);
            throw new InvalidOperationException("Unable to read Asana task id from response.");
        }

        var mapping = new TicketSyncMapping
        {
            FreshdeskTicketId = ticket.Id,
            AsanaTaskId = created.Data.Gid,
            SyncedAtUtc = DateTime.UtcNow
        };

        await mappingRepository.SaveAsync(mapping, cancellationToken);

        logger.LogInformation("Synced Freshdesk ticket {TicketId} to Asana task {TaskId}", ticket.Id, created.Data.Gid);

        return created.Data.Gid;
    }

    public async Task<string> SyncFromFreshdeskTicketIdAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        var existing = mappingRepository.GetByFreshdeskId(ticketId);
        if (existing != null)
        {
            logger.LogInformation("Mapping already exists for Freshdesk ticket {TicketId} -> Asana task {TaskId}", ticketId, existing.AsanaTaskId);
            return existing.AsanaTaskId;
        }

        var response = await freshdeskClient.GetTicketById(ticketId, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fdTicket = await response.Content.ReadFromJsonAsync<FreshdeskTicket>(cancellationToken: cancellationToken);
        if (fdTicket == null)
        {
            throw new InvalidOperationException($"Unable to deserialize Freshdesk ticket {ticketId}");
        }

        return await SyncFromFreshdeskTicketAsync(fdTicket, cancellationToken);
    }

    private sealed class AsanaTaskCreatedResponse
    {
        public AsanaTaskData? Data { get; set; }
    }

    private sealed class AsanaTaskData
    {
        public string Gid { get; set; } = string.Empty;
    }
}

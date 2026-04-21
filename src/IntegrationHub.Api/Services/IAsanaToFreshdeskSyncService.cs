namespace IntegrationHub.Api.Services;

public interface IAsanaToFreshdeskSyncService
{
    Task SyncTaskToFreshdeskAsync(string asanaTaskId, CancellationToken cancellationToken = default);
}

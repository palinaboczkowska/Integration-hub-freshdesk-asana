using IntegrationHub.Api.Data;
using IntegrationHub.Api.Data.Entities;
using IntegrationHub.Api.Models;

namespace IntegrationHub.Api.Repositories;

public class EfMappingRepository(IntegrationHubDbContext dbContext) : IMappingRepository
{
    public TicketSyncMapping? GetByFreshdeskId(long ticketId)
    {
        var entity = dbContext.TicketSyncMappings
            .FirstOrDefault(m => m.FreshdeskTicketId == ticketId);

        return entity == null ? null : ToModel(entity);
    }

    public TicketSyncMapping? GetByAsanaTaskId(string taskId)
    {
        var entity = dbContext.TicketSyncMappings
            .FirstOrDefault(m => m.AsanaTaskId == taskId);

        return entity == null ? null : ToModel(entity);
    }

    public async Task SaveAsync(TicketSyncMapping mapping, CancellationToken cancellationToken = default)
    {
        var entity = dbContext.TicketSyncMappings
            .FirstOrDefault(m => m.FreshdeskTicketId == mapping.FreshdeskTicketId);

        if (entity == null)
        {
            entity = new TicketSyncMappingEntity
            {
                FreshdeskTicketId = mapping.FreshdeskTicketId,
                AsanaTaskId = mapping.AsanaTaskId,
                SyncedAtUtc = mapping.SyncedAtUtc
            };
            dbContext.TicketSyncMappings.Add(entity);
        }
        else
        {
            entity.AsanaTaskId = mapping.AsanaTaskId;
            entity.SyncedAtUtc = mapping.SyncedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TicketSyncMapping ToModel(TicketSyncMappingEntity entity) =>
        new()
        {
            FreshdeskTicketId = entity.FreshdeskTicketId,
            AsanaTaskId = entity.AsanaTaskId,
            SyncedAtUtc = entity.SyncedAtUtc
        };
}

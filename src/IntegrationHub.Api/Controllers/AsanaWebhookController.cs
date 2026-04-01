using IntegrationHub.Api.Models.Webhooks;
using IntegrationHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
public class AsanaWebhookController(
    IAsanaToFreshdeskSyncService syncService,
    ILogger<AsanaWebhookController> logger)
    : ControllerBase
{
    private readonly IAsanaToFreshdeskSyncService _syncService = syncService;
    private readonly ILogger<AsanaWebhookController> _logger = logger;

    [HttpPost("/webhooks/asana")]
    public async Task<IActionResult> HandleWebhookAsync(
        [FromBody] AsanaWebhookEnvelope payload,
        CancellationToken cancellationToken)
    {
        // 1) Challenge response
        if (!string.IsNullOrEmpty(payload.Challenge))
        {
            _logger.LogInformation("Responding to Asana challenge");
            return Ok(new { challenge = payload.Challenge });
        }

        // 2) No events
        if (payload.Events == null || payload.Events.Count == 0)
        {
            _logger.LogInformation("Asana webhook received with no events");
            return Ok();
        }

        // 3) Process events
        foreach (var ev in payload.Events)
        {
            if (ev.Resource.Resource_Type == "task")
            {
                _logger.LogInformation(
                    "Processing Asana event: Task {TaskId}, Action {Action}, Field {Field}",
                    ev.Resource.Gid,
                    ev.Action,
                    ev.Change?.Field
                );

                await _syncService.SyncTaskToFreshdeskAsync(ev.Resource.Gid, cancellationToken);
            }
        }

        return Ok();
    }
}

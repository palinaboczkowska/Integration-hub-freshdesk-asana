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
    [HttpPost("/webhooks/asana")]
    public async Task<IActionResult> HandleWebhookAsync(
        [FromBody] AsanaWebhookEnvelope payload,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(payload.Challenge))
        {
            logger.LogInformation("Responding to Asana challenge");
            return Ok(new { challenge = payload.Challenge });
        }

        if (payload.Events == null || payload.Events.Count == 0)
        {
            logger.LogInformation("Asana webhook received with no events");
            return Ok();
        }

        foreach (var ev in payload.Events)
        {
            if (ev.Resource.ResourceType == "task")
            {
                logger.LogInformation(
                    "Processing Asana event: Task {TaskId}, Action {Action}, Field {Field}",
                    ev.Resource.Gid,
                    ev.Action,
                    ev.Change?.Field);

                await syncService.SyncTaskToFreshdeskAsync(ev.Resource.Gid, cancellationToken);
            }
        }

        return Ok();
    }
}

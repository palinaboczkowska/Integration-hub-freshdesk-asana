using IntegrationHub.Api.Models;
using IntegrationHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
public class FreshdeskWebhookController(ITicketSyncService ticketSyncService, ILogger<FreshdeskWebhookController> logger)
    : ControllerBase
{
    public class FreshdeskWebhookPayload
    {
        public FreshdeskTicket Ticket { get; set; } = new();
    }

    [HttpPost("/webhooks/freshdesk/ticket")]
    public async Task<IActionResult> HandleTicketAsync([FromBody] FreshdeskWebhookPayload payload, CancellationToken cancellationToken)
    {
        if (payload?.Ticket == null)
        {
            return BadRequest("Missing ticket data in webhook payload.");
        }

        logger.LogInformation("Received Freshdesk ticket webhook for ticket {TicketId}", payload.Ticket.Id);

        var asanaTaskId = await ticketSyncService.SyncFromFreshdeskTicketAsync(payload.Ticket, cancellationToken);

        return Ok(new { asanaTaskId });
    }
}

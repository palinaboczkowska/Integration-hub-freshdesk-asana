using IntegrationHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IntegrationsController(IIntegrationService integrationService, ILogger<IntegrationsController> logger)
    : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        logger.LogInformation("Fetching available integrations");
        return Ok(integrationService.GetAvailableIntegrations());
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        logger.LogInformation("Fetching integration hub settings");
        return Ok(integrationService.GetSettings());
    }
}

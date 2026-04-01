namespace IntegrationHub.Api.Models.Webhooks;

public class AsanaWebhookEnvelope
{
    public string? Challenge { get; set; }
    public List<AsanaWebhookEvent>? Events { get; set; }
}

public class AsanaWebhookEvent
{
    public AsanaResource Resource { get; set; } = new();
    public string Action { get; set; } = string.Empty;
    public AsanaChange? Change { get; set; }
}

public class AsanaResource
{
    public string Gid { get; set; } = string.Empty;
    public string Resource_Type { get; set; } = string.Empty;
}

public class AsanaChange
{
    public string Field { get; set; } = string.Empty;
}

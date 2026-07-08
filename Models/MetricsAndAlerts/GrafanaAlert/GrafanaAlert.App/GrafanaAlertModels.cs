namespace GrafanaAlertApp;

public sealed class GrafanaWebhookPayload
{
    public string? Receiver { get; init; }

    public string? Status { get; init; }

    public string? Title { get; init; }

    public string? Message { get; init; }

    public string? ExternalUrl { get; init; }

    public IReadOnlyDictionary<string, string>? CommonLabels { get; init; }

    public IReadOnlyDictionary<string, string>? CommonAnnotations { get; init; }

    public IReadOnlyList<GrafanaAlertItem> Alerts { get; init; } = Array.Empty<GrafanaAlertItem>();
}

public sealed class GrafanaAlertItem
{
    public string? Status { get; init; }

    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    public IReadOnlyDictionary<string, string>? Annotations { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public string? GeneratorUrl { get; init; }

    public string? Fingerprint { get; init; }
}

public sealed class InternalAlertMessage
{
    public required IReadOnlyList<string> ToUsers { get; init; }

    public int AgentId { get; init; }

    public required string Content { get; init; }
}

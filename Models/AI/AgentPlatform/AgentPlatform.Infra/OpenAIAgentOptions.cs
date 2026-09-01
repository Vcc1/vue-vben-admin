using System.ComponentModel.DataAnnotations;

namespace AgentPlatform.Infra;

public sealed class OpenAIAgentOptions
{
    public const string SectionName = "AgentPlatform:OpenAI";

    [Required]
    public string Model { get; set; } = "gpt-5.6-sol";

    public string? ApiKey { get; set; }

    public string? Endpoint { get; set; }

    public bool EnableWebSearch { get; set; } = true;

    [Range(30, 1_800)]
    public int NetworkTimeoutSeconds { get; set; } = 600;

    [Range(0, 5)]
    public int MaxRetries { get; set; } = 1;

    [Range(1, 10_080)]
    public int SessionTtlMinutes { get; set; } = 120;

    public string Instructions { get; set; } =
        "You are an internal operations assistant. Use the available business tools for internal data. " +
        "When current public information is needed, use web search and preserve source citations. " +
        "Do not invent facts, identifiers, prices, or operational results.";
}

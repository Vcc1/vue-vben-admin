using System.Text.Json.Serialization;

namespace Infrastructure.HTTP.WorkWechatAlert;

public sealed class WorkWechatTemplateMessageRequest
{
    [JsonPropertyName("ToUsers")]
    public required IReadOnlyList<string> ToUsers { get; init; }

    [JsonPropertyName("Agentid")]
    public int Agentid { get; init; }

    [JsonPropertyName("ToDept")]
    public IReadOnlyList<int>? ToDept { get; init; }

    [JsonPropertyName("ToTag")]
    public IReadOnlyList<int>? ToTag { get; init; }

    [JsonPropertyName("Urls")]
    public IReadOnlyDictionary<string, string>? Urls { get; init; }

    [JsonPropertyName("Content")]
    public required string Content { get; init; }
}

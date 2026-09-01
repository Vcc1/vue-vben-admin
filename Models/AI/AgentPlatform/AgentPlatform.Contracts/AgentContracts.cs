using System.Text.Json.Serialization;

namespace AgentPlatform.Contracts;

public sealed record AgentRequest
{
    public string Message { get; init; } = string.Empty;

    public string AgentName { get; init; } = "operations";

    public string? ConversationId { get; init; }

    public bool EnableWebSearch { get; init; } = true;

    public Dictionary<string, string>? Metadata { get; init; }

    public AgentJsonResponseFormat? JsonResponseFormat { get; init; }

    [JsonIgnore]
    public AgentConversationStorageScope? StorageScope { get; init; }
}

public sealed record AgentConversationStorageScope
{
    public required string BusinessType { get; init; }

    public required string BusinessKey { get; init; }

    public required string OperationType { get; init; }

    public required string RunId { get; init; }
}

public sealed record AgentJsonResponseFormat
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Schema { get; init; }
}

public sealed record AgentResponse
{
    public required string ConversationId { get; init; }

    public required string AgentName { get; init; }

    public required string Text { get; init; }

    public string? ResponseId { get; init; }

    public string? FinishReason { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public IReadOnlyList<string> SearchQueries { get; init; } = [];

    public IReadOnlyList<AgentSource> Sources { get; init; } = [];

    public AgentUsage? Usage { get; init; }
}

public sealed record AgentSource
{
    public required string Url { get; init; }

    public string? Title { get; init; }

    public string? Snippet { get; init; }
}

public sealed record AgentUsage
{
    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? TotalTokens { get; init; }

    public long? ReasoningTokens { get; init; }
}

public sealed record AgentConversationSummary
{
    public required string ConversationId { get; init; }

    public required string AgentName { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required int MessageCount { get; init; }

    public string? LastMessage { get; init; }
}

public sealed record AgentConversation
{
    public int FileSchemaVersion { get; init; } = 1;

    public required string ConversationId { get; init; }

    public required string AgentName { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<AgentConversationMessage> Messages { get; init; } = [];
}

public sealed record AgentConversationMessage
{
    public required string MessageId { get; init; }

    public required string Role { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public bool? WebSearchEnabled { get; init; }

    public string? ResponseId { get; init; }

    public string? FinishReason { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public IReadOnlyList<string> SearchQueries { get; init; } = [];

    public IReadOnlyList<AgentSource> Sources { get; init; } = [];

    public AgentUsage? Usage { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public enum AgentStreamEventKind
{
    Started,
    Text,
    Source,
    Completed,
    Error
}

public sealed record AgentStreamEvent
{
    public required AgentStreamEventKind Kind { get; init; }

    public required string ConversationId { get; init; }

    public string? Text { get; init; }

    public AgentSource? Source { get; init; }

    public AgentUsage? Usage { get; init; }

    public string? Error { get; init; }
}

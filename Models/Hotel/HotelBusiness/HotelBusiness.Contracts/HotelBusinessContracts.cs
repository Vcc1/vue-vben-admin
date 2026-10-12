using AgentPlatform.Contracts;

namespace HotelBusiness.Contracts;

public static class HotelBusinessOperations
{
    public const string HotelResearch = "hotel-research";
}

public enum HotelOperationRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record HotelProcessorDescriptor
{
    public required string Name { get; init; }

    public required string ProcessorVersion { get; init; }

    public required string PromptVersion { get; init; }

    public required int ResultSchemaVersion { get; init; }

    public required string AgentName { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public bool WebSearchEnabled { get; init; }
}

public sealed record HotelBusinessRecentItem
{
    public required string MTHotelCode { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required string LastOperationType { get; init; }

    public required HotelOperationRunStatus LastRunStatus { get; init; }

    public int? LatestSuccessfulVersion { get; init; }

    public IReadOnlyList<HotelBusinessRecentOperation> Operations { get; init; } = [];
}

public sealed record HotelBusinessRecentOperation
{
    public required string OperationType { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required HotelOperationRunStatus LastRunStatus { get; init; }

    public int? LatestSuccessfulVersion { get; init; }
}

public sealed record HotelBusinessDetail
{
    public int FileSchemaVersion { get; init; } = 1;

    public required string MTHotelCode { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<HotelBusinessOperationSummary> Operations { get; init; } = [];
}

public sealed record HotelBusinessOperationSummary
{
    public int FileSchemaVersion { get; init; } = 1;

    public required string MTHotelCode { get; init; }

    public required string OperationType { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public int? LatestSuccessfulVersion { get; init; }

    public IReadOnlyList<HotelBusinessResultVersionSummary> Versions { get; init; } = [];

    public IReadOnlyList<HotelBusinessRunSummary> Runs { get; init; } = [];
}

public sealed record HotelBusinessResultVersionSummary
{
    public required int ResultVersion { get; init; }

    public required string RunId { get; init; }

    public required string ConversationId { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required HotelProcessorDescriptor Processor { get; init; }
}

public sealed record HotelBusinessRunSummary
{
    public int FileSchemaVersion { get; init; } = 1;

    public required string MTHotelCode { get; init; }

    public required string OperationType { get; init; }

    public required string RunId { get; init; }

    public required string ConversationId { get; init; }

    public required HotelOperationRunStatus Status { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public int? ResultVersion { get; init; }

    public required HotelProcessorDescriptor Processor { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record HotelBusinessRunConversation
{
    public required HotelBusinessRunSummary Run { get; init; }

    public required AgentConversation Conversation { get; init; }
}

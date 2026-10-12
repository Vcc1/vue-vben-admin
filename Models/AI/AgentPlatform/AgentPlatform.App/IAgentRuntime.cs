using AgentPlatform.Contracts;
namespace AgentPlatform.App;

public interface IAgentRuntime
{
    Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentSessionStore
{
    ValueTask<string?> GetAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        string conversationId,
        string serializedSession,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);
}

public interface IAgentConversationStore
{
    Task<IReadOnlyList<AgentConversationSummary>> ListAsync(
        string? query,
        int take,
        CancellationToken cancellationToken = default);

    Task<AgentConversation?> GetAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);

    Task AppendMessageAsync(
        string conversationId,
        string agentName,
        AgentConversationMessage message,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default);
}

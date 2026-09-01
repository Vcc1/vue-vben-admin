namespace AgentPlatform.Contracts;

public interface IAgentConversationService
{
    Task<IReadOnlyList<AgentConversationSummary>> ListAsync(
        string? query = null,
        int take = 200,
        CancellationToken cancellationToken = default);

    Task<AgentConversation?> GetAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}

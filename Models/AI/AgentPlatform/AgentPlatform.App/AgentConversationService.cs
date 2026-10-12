using AgentPlatform.Contracts;

namespace AgentPlatform.App;

public sealed class AgentConversationService(
    IAgentConversationStore conversationStore,
    IAgentSessionStore sessionStore) : IAgentConversationService
{
    public Task<IReadOnlyList<AgentConversationSummary>> ListAsync(
        string? query = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 500.");
        }

        return conversationStore.ListAsync(query?.Trim(), take, cancellationToken);
    }

    public Task<AgentConversation?> GetAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        AgentService.ValidateConversationId(conversationId);
        return conversationStore.GetAsync(conversationId, cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        AgentService.ValidateConversationId(conversationId);
        await sessionStore.DeleteAsync(conversationId, cancellationToken: cancellationToken);
        return await conversationStore.DeleteAsync(conversationId, cancellationToken: cancellationToken);
    }
}

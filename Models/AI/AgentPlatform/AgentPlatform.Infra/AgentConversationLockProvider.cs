using System.Collections.Concurrent;

namespace AgentPlatform.Infra;

internal sealed class AgentConversationLockProvider
{
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public SemaphoreSlim Get(string conversationId)
        => _locks.GetOrAdd(conversationId, static _ => new SemaphoreSlim(1, 1));
}

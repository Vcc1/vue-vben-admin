using AgentPlatform.Contracts;
using Xunit;

namespace AgentPlatform.App.Tests;

public sealed class AgentConversationServiceTests
{
    [Fact]
    public async Task ListAsync_ForwardsNormalizedQuery()
    {
        var store = new FakeConversationStore();
        var service = new AgentConversationService(store, new FakeSessionStore());

        await service.ListAsync("  hotel  ", 25);

        Assert.Equal("hotel", store.LastQuery);
        Assert.Equal(25, store.LastTake);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSessionAndConversation()
    {
        var conversationStore = new FakeConversationStore { DeleteResult = true };
        var sessionStore = new FakeSessionStore();
        var service = new AgentConversationService(conversationStore, sessionStore);

        var deleted = await service.DeleteAsync("conversation-1");

        Assert.True(deleted);
        Assert.Equal("conversation-1", sessionStore.DeletedConversationId);
        Assert.Equal("conversation-1", conversationStore.DeletedConversationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ListAsync_RejectsInvalidTake(int take)
    {
        var service = new AgentConversationService(
            new FakeConversationStore(),
            new FakeSessionStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(take: take));
    }

    sealed class FakeConversationStore : IAgentConversationStore
    {
        public string? LastQuery { get; private set; }

        public int LastTake { get; private set; }

        public bool DeleteResult { get; init; }

        public string? DeletedConversationId { get; private set; }

        public Task<IReadOnlyList<AgentConversationSummary>> ListAsync(
            string? query,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<AgentConversationSummary>>([]);
        }

        public Task<AgentConversation?> GetAsync(
            string conversationId,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AgentConversation?>(null);

        public Task AppendMessageAsync(
            string conversationId,
            string agentName,
            AgentConversationMessage message,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(
            string conversationId,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
        {
            DeletedConversationId = conversationId;
            return Task.FromResult(DeleteResult);
        }
    }

    sealed class FakeSessionStore : IAgentSessionStore
    {
        public string? DeletedConversationId { get; private set; }

        public ValueTask<string?> GetAsync(
            string conversationId,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>(null);

        public ValueTask SaveAsync(
            string conversationId,
            string serializedSession,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(
            string conversationId,
            AgentConversationStorageScope? scope = null,
            CancellationToken cancellationToken = default)
        {
            DeletedConversationId = conversationId;
            return ValueTask.CompletedTask;
        }
    }
}

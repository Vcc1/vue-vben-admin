using AgentPlatform.Contracts;
using System.Runtime.CompilerServices;
using Xunit;

namespace AgentPlatform.App.Tests;

public sealed class AgentServiceTests
{
    [Fact]
    public async Task RunAsync_ForwardsValidRequestToRuntime()
    {
        var expected = new AgentResponse
        {
            ConversationId = "conversation-1",
            AgentName = "operations",
            Text = "answer"
        };
        var runtime = new FakeAgentRuntime(expected);
        var service = new AgentService(runtime);
        var request = new AgentRequest { Message = "search current hotel news" };

        var actual = await service.RunAsync(request);

        Assert.Same(expected, actual);
        Assert.Same(request, runtime.LastRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_RejectsBlankMessage(string message)
    {
        var service = new AgentService(new FakeAgentRuntime());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(new AgentRequest { Message = message }));
    }

    [Fact]
    public async Task RunAsync_RejectsUnsafeAgentName()
    {
        var service = new AgentService(new FakeAgentRuntime());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(new AgentRequest
            {
                AgentName = "../../invalid",
                Message = "hello"
            }));
    }

    [Fact]
    public async Task RunAsync_RejectsUnsafeConversationId()
    {
        var service = new AgentService(new FakeAgentRuntime());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(new AgentRequest
            {
                ConversationId = "../invalid",
                Message = "hello"
            }));
    }

    [Fact]
    public async Task RunAsync_ForwardsValidJsonResponseFormat()
    {
        var runtime = new FakeAgentRuntime();
        var service = new AgentService(runtime);

        await service.RunAsync(new AgentRequest
        {
            Message = "return structured data",
            JsonResponseFormat = new AgentJsonResponseFormat
            {
                Name = "structured_result",
                Schema = "{\"type\":\"object\"}"
            }
        });

        Assert.NotNull(runtime.LastRequest?.JsonResponseFormat);
    }

    [Fact]
    public async Task RunAsync_RejectsInvalidJsonResponseSchema()
    {
        var service = new AgentService(new FakeAgentRuntime());

        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(new AgentRequest
        {
            Message = "return structured data",
            JsonResponseFormat = new AgentJsonResponseFormat
            {
                Name = "structured_result",
                Schema = "not-json"
            }
        }));
    }

    sealed class FakeAgentRuntime(AgentResponse? response = null) : IAgentRuntime
    {
        readonly AgentResponse _response = response ?? new AgentResponse
        {
            ConversationId = "conversation-1",
            AgentName = "operations",
            Text = string.Empty
        };

        public AgentRequest? LastRequest { get; private set; }

        public Task<AgentResponse> RunAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }

        public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            await Task.CompletedTask;
            yield break;
        }
    }
}

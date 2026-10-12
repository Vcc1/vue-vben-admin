namespace AgentPlatform.Contracts;

public interface IAgentService
{
    Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}

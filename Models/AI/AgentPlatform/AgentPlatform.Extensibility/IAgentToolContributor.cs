using Microsoft.Extensions.AI;

namespace AgentPlatform.Extensibility;

/// <summary>
/// Contributes business tools to selected agents without making AgentPlatform depend on business modules.
/// </summary>
public interface IAgentToolContributor
{
    bool Supports(string agentName);

    IReadOnlyList<AITool> CreateTools();
}

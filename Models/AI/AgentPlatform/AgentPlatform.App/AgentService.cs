using AgentPlatform.Contracts;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPlatform.App;

public sealed partial class AgentService(IAgentRuntime runtime) : IAgentService
{
    const int MaximumMessageLength = 50_000;

    public Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return runtime.RunAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Validate(request);

        await foreach (var item in runtime.RunStreamingAsync(request, cancellationToken))
        {
            yield return item;
        }
    }

    static void Validate(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Agent message is required.", nameof(request));
        }

        if (request.Message.Length > MaximumMessageLength)
        {
            throw new ArgumentException(
                $"Agent message cannot exceed {MaximumMessageLength} characters.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AgentName) || !AgentNameRegex().IsMatch(request.AgentName))
        {
            throw new ArgumentException(
                "Agent name must contain 1-64 letters, numbers, underscores, or hyphens.",
                nameof(request));
        }

        if (request.ConversationId is not null)
        {
            ValidateConversationId(request.ConversationId);
        }

        ValidateJsonResponseFormat(request.JsonResponseFormat);
    }

    static void ValidateJsonResponseFormat(AgentJsonResponseFormat? format)
    {
        if (format is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(format.Name) || !AgentNameRegex().IsMatch(format.Name))
        {
            throw new ArgumentException(
                "JSON response format name must contain 1-64 letters, numbers, underscores, or hyphens.",
                nameof(format));
        }

        if (string.IsNullOrWhiteSpace(format.Schema) || format.Schema.Length > 100_000)
        {
            throw new ArgumentException(
                "JSON response schema is required and cannot exceed 100000 characters.",
                nameof(format));
        }

        try
        {
            using var _ = JsonDocument.Parse(format.Schema);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("JSON response schema must be valid JSON.", nameof(format), exception);
        }
    }

    internal static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)
            || !ConversationIdRegex().IsMatch(conversationId))
        {
            throw new ArgumentException(
                "Conversation id must contain 1-128 letters, numbers, underscores, or hyphens.",
                nameof(conversationId));
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex AgentNameRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ConversationIdRegex();
}

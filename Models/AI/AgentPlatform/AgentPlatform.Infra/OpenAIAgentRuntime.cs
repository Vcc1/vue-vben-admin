using AgentPlatform.App;
using AgentPlatform.Contracts;
using AgentPlatform.Extensibility;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContractAgentResponse = AgentPlatform.Contracts.AgentResponse;

#pragma warning disable OPENAI001 // Responses API types are still annotated experimental by the OpenAI .NET SDK.

namespace AgentPlatform.Infra;

internal sealed class OpenAIAgentRuntime(
    IOptionsMonitor<OpenAIAgentOptions> options,
    IEnumerable<IAgentToolContributor> toolContributors,
    IAgentSessionStore sessionStore,
    IAgentConversationStore conversationStore,
    AgentConversationLockProvider lockProvider,
    ILoggerFactory loggerFactory,
    IServiceProvider services) : IAgentRuntime
{
    public async Task<ContractAgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversationId = ResolveConversationId(request.ConversationId);
        var conversationLock = lockProvider.Get(conversationId);
        await conversationLock.WaitAsync(cancellationToken);

        try
        {
            var runtimeModel = options.CurrentValue.Model;
            var agent = CreateAgent(request);
            var session = await RestoreSessionAsync(
                agent,
                conversationId,
                request.StorageScope,
                cancellationToken);
            await conversationStore.AppendMessageAsync(
                conversationId,
                request.AgentName,
                CreateUserMessage(request),
                request.StorageScope,
                cancellationToken);
            var response = await agent.RunAsync(
                request.Message,
                session,
                CreateRunOptions(request),
                cancellationToken);

            var mappedResponse = MapResponse(request.AgentName, conversationId, runtimeModel, response);
            await SaveSessionAsync(
                agent,
                session,
                conversationId,
                request.StorageScope,
                cancellationToken);
            await conversationStore.AppendMessageAsync(
                conversationId,
                request.AgentName,
                CreateAssistantMessage(mappedResponse),
                request.StorageScope,
                cancellationToken);
            return mappedResponse;
        }
        finally
        {
            conversationLock.Release();
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversationId = ResolveConversationId(request.ConversationId);
        var conversationLock = lockProvider.Get(conversationId);
        await conversationLock.WaitAsync(cancellationToken);

        try
        {
            var agent = CreateAgent(request);
            var session = await RestoreSessionAsync(
                agent,
                conversationId,
                request.StorageScope,
                cancellationToken);
            var emittedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sources = new List<AgentSource>();
            var queries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var text = new StringBuilder();
            AgentUsage? usage = null;

            await conversationStore.AppendMessageAsync(
                conversationId,
                request.AgentName,
                CreateUserMessage(request),
                request.StorageScope,
                cancellationToken);

            yield return new AgentStreamEvent
            {
                Kind = AgentStreamEventKind.Started,
                ConversationId = conversationId
            };

            await foreach (var update in agent.RunStreamingAsync(
                request.Message,
                session,
                CreateRunOptions(request),
                cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    text.Append(update.Text);
                    yield return new AgentStreamEvent
                    {
                        Kind = AgentStreamEventKind.Text,
                        ConversationId = conversationId,
                        Text = update.Text
                    };
                }

                foreach (var source in ExtractSources(update.Contents))
                {
                    if (emittedSources.Add(source.Url))
                    {
                        sources.Add(source);
                        yield return new AgentStreamEvent
                        {
                            Kind = AgentStreamEventKind.Source,
                            ConversationId = conversationId,
                            Source = source
                        };
                    }
                }

                foreach (var query in ExtractSearchQueries(update.Contents))
                {
                    queries.Add(query);
                }

                var usageContent = update.Contents.OfType<UsageContent>().LastOrDefault();
                if (usageContent is not null)
                {
                    usage = MapUsage(usageContent.Details);
                }
            }

            await SaveSessionAsync(
                agent,
                session,
                conversationId,
                request.StorageScope,
                cancellationToken);
            await conversationStore.AppendMessageAsync(
                conversationId,
                request.AgentName,
                new AgentConversationMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = "assistant",
                    Text = text.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    SearchQueries = queries.ToArray(),
                    Sources = sources,
                    Usage = usage
                },
                request.StorageScope,
                cancellationToken);

            yield return new AgentStreamEvent
            {
                Kind = AgentStreamEventKind.Completed,
                ConversationId = conversationId,
                Usage = usage
            };
        }
        finally
        {
            conversationLock.Release();
        }
    }

    AIAgent CreateAgent(AgentRequest request)
    {
        var currentOptions = options.CurrentValue;
        var apiKey = string.IsNullOrWhiteSpace(currentOptions.ApiKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : currentOptions.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AgentPlatformConfigurationException(
                "OpenAI API key is not configured. Set OPENAI_API_KEY or AgentPlatform:OpenAI:ApiKey.");
        }

        var tools = toolContributors
            .Where(contributor => contributor.Supports(request.AgentName))
            .SelectMany(contributor => contributor.CreateTools())
            .ToList();

        if (request.EnableWebSearch && currentOptions.EnableWebSearch)
        {
            tools.Add(new HostedWebSearchTool());
        }

        EnsureUniqueToolNames(tools);

        var client = CreateResponsesClient(apiKey, currentOptions);
        return client.AsAIAgent(
            model: currentOptions.Model,
            instructions: currentOptions.Instructions,
            name: request.AgentName,
            description: "Mengtu operations agent with business tools and OpenAI web search.",
            tools: tools,
            loggerFactory: loggerFactory,
            services: services);
    }

    static ResponsesClient CreateResponsesClient(string apiKey, OpenAIAgentOptions options)
    {
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(options.NetworkTimeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(options.MaxRetries)
        };

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            if (!Uri.TryCreate(options.Endpoint.Trim(), UriKind.Absolute, out var endpointUri)
                || endpointUri.Scheme is not ("http" or "https"))
            {
                throw new AgentPlatformConfigurationException(
                    "AgentPlatform:OpenAI:Endpoint must be an absolute HTTP or HTTPS URL.");
            }

            if (endpointUri.AbsolutePath is "" or "/")
            {
                endpointUri = new Uri(endpointUri, "/v1");
            }

            clientOptions.Endpoint = endpointUri;
        }

        return new ResponsesClient(
            new ApiKeyCredential(apiKey),
            clientOptions);
    }

    async Task<AgentSession> RestoreSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentConversationStorageScope? scope,
        CancellationToken cancellationToken)
    {
        var serializedSession = await sessionStore.GetAsync(
            conversationId,
            scope,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(serializedSession))
        {
            return await agent.CreateSessionAsync(cancellationToken);
        }

        using var document = JsonDocument.Parse(serializedSession);
        return await agent.DeserializeSessionAsync(
            document.RootElement,
            cancellationToken: cancellationToken);
    }

    async Task SaveSessionAsync(
        AIAgent agent,
        AgentSession session,
        string conversationId,
        AgentConversationStorageScope? scope,
        CancellationToken cancellationToken)
    {
        var serializedSession = await agent.SerializeSessionAsync(
            session,
            cancellationToken: cancellationToken);
        await sessionStore.SaveAsync(
            conversationId,
            serializedSession.GetRawText(),
            scope,
            cancellationToken);
    }

    static ContractAgentResponse MapResponse(
        string agentName,
        string conversationId,
        string model,
        Microsoft.Agents.AI.AgentResponse response)
    {
        var contents = response.Messages.SelectMany(message => message.Contents).ToArray();
        var queries = contents
            .OfType<WebSearchToolCallContent>()
            .SelectMany(content => content.Queries ?? [])
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ContractAgentResponse
        {
            ConversationId = conversationId,
            AgentName = agentName,
            Text = response.Text,
            ResponseId = response.ResponseId,
            FinishReason = response.FinishReason?.ToString(),
            Provider = "openai-responses",
            Model = model,
            SearchQueries = queries,
            Sources = ExtractSources(contents),
            Usage = response.Usage is null ? null : MapUsage(response.Usage)
        };
    }

    static AgentRunOptions? CreateRunOptions(AgentRequest request)
    {
        if (request.JsonResponseFormat is not { } format)
        {
            return null;
        }

        using var document = JsonDocument.Parse(format.Schema);
        return new AgentRunOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                document.RootElement.Clone(),
                format.Name,
                format.Description)
        };
    }

    static AgentConversationMessage CreateUserMessage(AgentRequest request)
        => new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Role = "user",
            Text = request.Message,
            CreatedAt = DateTimeOffset.UtcNow,
            WebSearchEnabled = request.EnableWebSearch,
            Metadata = request.Metadata is null
                ? null
                : new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal)
        };

    static AgentConversationMessage CreateAssistantMessage(ContractAgentResponse response)
        => new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Role = "assistant",
            Text = response.Text,
            CreatedAt = DateTimeOffset.UtcNow,
            ResponseId = response.ResponseId,
            FinishReason = response.FinishReason,
            Provider = response.Provider,
            Model = response.Model,
            SearchQueries = response.SearchQueries,
            Sources = response.Sources,
            Usage = response.Usage
        };

    static IEnumerable<string> ExtractSearchQueries(IEnumerable<AIContent> contents)
        => contents
            .OfType<WebSearchToolCallContent>()
            .SelectMany(content => content.Queries ?? [])
            .Where(query => !string.IsNullOrWhiteSpace(query));

    static IReadOnlyList<AgentSource> ExtractSources(IEnumerable<AIContent> contents)
    {
        var sources = new Dictionary<string, AgentSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var content in contents)
        {
            foreach (var citation in content.Annotations?.OfType<CitationAnnotation>() ?? [])
            {
                if (citation.Url is null)
                {
                    continue;
                }

                var url = citation.Url.ToString();
                sources.TryAdd(url, new AgentSource
                {
                    Url = url,
                    Title = citation.Title,
                    Snippet = citation.Snippet
                });
            }

            if (content is not WebSearchToolResultContent searchResult)
            {
                continue;
            }

            foreach (var uriContent in searchResult.Outputs?.OfType<UriContent>() ?? [])
            {
                var url = uriContent.Uri.ToString();
                string? title = null;
                if (uriContent.AdditionalProperties is { } properties
                    && properties.TryGetValue("title", out var titleValue))
                {
                    title = titleValue?.ToString();
                }

                sources.TryAdd(url, new AgentSource
                {
                    Url = url,
                    Title = title
                });
            }
        }

        return sources.Values.ToArray();
    }

    static AgentUsage MapUsage(UsageDetails usage)
        => new()
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount,
            ReasoningTokens = usage.ReasoningTokenCount
        };

    static string ResolveConversationId(string? conversationId)
        => string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();

    static void EnsureUniqueToolNames(IReadOnlyCollection<AITool> tools)
    {
        var duplicate = tools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.Name))
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate agent tool name: {duplicate.Key}.");
        }
    }
}

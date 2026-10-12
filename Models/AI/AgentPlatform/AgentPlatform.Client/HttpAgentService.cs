using AgentPlatform.Contracts;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentPlatform.Client;

public sealed class HttpAgentService(HttpClient httpClient) : IAgentService
{
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            BuildPath(request.AgentName, streaming: false),
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AgentResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Agent API returned an empty response.");
    }

    public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPath(request.AgentName, streaming: true))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<AgentStreamEvent>(line, JsonOptions)
                ?? throw new InvalidOperationException("Agent API returned an invalid stream event.");
            yield return item;
        }
    }

    static string BuildPath(string agentName, bool streaming)
    {
        var escapedName = Uri.EscapeDataString(agentName);
        return streaming
            ? $"api/agents/{escapedName}/runs/stream"
            : $"api/agents/{escapedName}/runs";
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

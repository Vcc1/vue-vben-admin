using AgentPlatform.App;
using AgentPlatform.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentPlatform.Infra;

internal sealed class FileAgentStorage(
    AgentStoragePaths paths,
    IOptionsMonitor<OpenAIAgentOptions> openAIOptions,
    ILogger<FileAgentStorage> logger) : IAgentSessionStore, IAgentConversationStore
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    async ValueTask<string?> IAgentSessionStore.GetAsync(
        string conversationId,
        AgentConversationStorageScope? scope,
        CancellationToken cancellationToken)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var file = paths.GetSessionFile(conversationId, scope);
            if (!File.Exists(file))
            {
                return null;
            }

            var expiresAt = File.GetLastWriteTimeUtc(file)
                .AddMinutes(openAIOptions.CurrentValue.SessionTtlMinutes);
            if (expiresAt <= DateTime.UtcNow)
            {
                File.Delete(file);
                DeleteDirectoryIfEmpty(paths.GetConversationDirectory(conversationId, scope));
                return null;
            }

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            if (scope is null)
            {
                return content;
            }

            try
            {
                var scopedFile = JsonSerializer.Deserialize<BusinessAgentSessionFile>(content, JsonOptions);
                return scopedFile?.Session.GetRawText() ?? content;
            }
            catch (JsonException)
            {
                // Read pre-wrapper scoped sessions for backward compatibility.
                return content;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask SaveAsync(
        string conversationId,
        string serializedSession,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var content = scope is null
                ? serializedSession
                : WrapBusinessSession(serializedSession);
            await WriteAtomicAsync(
                paths.GetSessionFile(conversationId, scope),
                content,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    async ValueTask IAgentSessionStore.DeleteAsync(
        string conversationId,
        AgentConversationStorageScope? scope,
        CancellationToken cancellationToken)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var file = paths.GetSessionFile(conversationId, scope);
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            DeleteDirectoryIfEmpty(paths.GetConversationDirectory(conversationId, scope));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AgentConversationSummary>> ListAsync(
        string? query,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.RootPath))
        {
            return [];
        }

        var conversations = new List<AgentConversationSummary>();
        foreach (var directory in Directory.EnumerateDirectories(paths.RootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conversation = await ReadConversationAsync(
                Path.Combine(directory, "conversation.json"),
                cancellationToken);
            if (conversation is null || !Matches(conversation, query))
            {
                continue;
            }

            conversations.Add(ToSummary(conversation));
        }

        return conversations
            .OrderByDescending(item => item.UpdatedAt)
            .Take(take)
            .ToArray();
    }

    public async Task<AgentConversation?> GetAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadConversationAsync(
                paths.GetConversationFile(conversationId, scope),
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendMessageAsync(
        string conversationId,
        string agentName,
        AgentConversationMessage message,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var file = paths.GetConversationFile(conversationId, scope);
            var conversation = await ReadConversationAsync(file, cancellationToken);
            var messages = conversation?.Messages.ToList() ?? [];
            messages.Add(message);

            var firstUserMessage = messages.FirstOrDefault(item => item.Role == "user")?.Text;
            var now = message.CreatedAt;
            conversation = new AgentConversation
            {
                ConversationId = conversationId,
                AgentName = conversation?.AgentName ?? agentName,
                Title = conversation?.Title ?? CreatePreview(firstUserMessage, 80, "新会话")!,
                CreatedAt = conversation?.CreatedAt ?? now,
                UpdatedAt = now,
                Messages = messages
            };

            await WriteAtomicAsync(
                file,
                JsonSerializer.Serialize(conversation, JsonOptions),
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(
        string conversationId,
        AgentConversationStorageScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(conversationId, scope);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var directory = paths.GetConversationDirectory(conversationId, scope);
            if (!Directory.Exists(directory))
            {
                return false;
            }

            Directory.Delete(directory, recursive: true);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    SemaphoreSlim GetLock(string conversationId, AgentConversationStorageScope? scope)
    {
        var key = scope is null
            ? conversationId
            : string.Join(':', scope.BusinessType, scope.BusinessKey, scope.OperationType, scope.RunId, conversationId);
        return _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
    }

    async Task<AgentConversation?> ReadConversationAsync(
        string file,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<AgentConversation>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            logger.LogWarning(exception, "Unable to read agent conversation file {ConversationFile}", file);
            return null;
        }
    }

    static bool Matches(AgentConversation conversation, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return conversation.ConversationId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || conversation.AgentName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || conversation.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || conversation.Messages.Any(message =>
                message.Text.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    static AgentConversationSummary ToSummary(AgentConversation conversation)
        => new()
        {
            ConversationId = conversation.ConversationId,
            AgentName = conversation.AgentName,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            MessageCount = conversation.Messages.Count,
            LastMessage = CreatePreview(conversation.Messages.LastOrDefault()?.Text, 160, null)
        };

    static string? CreatePreview(string? text, int maximumLength, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var normalized = string.Join(' ', text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, maximumLength - 1), "…");
    }

    static async Task WriteAtomicAsync(
        string targetFile,
        string content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetFile)!;
        Directory.CreateDirectory(directory);
        var temporaryFile = Path.Combine(directory, $".{Path.GetFileName(targetFile)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporaryFile, content, cancellationToken);
            File.Move(temporaryFile, targetFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    static void DeleteDirectoryIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    static string WrapBusinessSession(string serializedSession)
    {
        using var document = JsonDocument.Parse(serializedSession);
        return JsonSerializer.Serialize(
            new BusinessAgentSessionFile { Session = document.RootElement.Clone() },
            JsonOptions);
    }

    sealed record BusinessAgentSessionFile
    {
        public int FileSchemaVersion { get; init; } = 1;

        public required JsonElement Session { get; init; }
    }
}

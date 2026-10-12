using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using AgentPlatform.Contracts;

namespace AgentPlatform.Infra;

internal sealed class AgentStoragePaths
{
    public AgentStoragePaths(
        IHostEnvironment environment,
        IOptions<AgentStorageOptions> options)
    {
        var configuredPath = options.Value.RootPath;
        RootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));

        var configuredBusinessPath = options.Value.BusinessRootPath;
        BusinessRootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredBusinessPath)
                ? configuredBusinessPath
                : Path.Combine(environment.ContentRootPath, configuredBusinessPath));
    }

    public string RootPath { get; }

    public string BusinessRootPath { get; }

    public string GetConversationDirectory(
        string conversationId,
        AgentConversationStorageScope? scope = null)
    {
        ValidateConversationId(conversationId);

        if (scope is not null)
        {
            return GetBusinessConversationDirectory(scope);
        }

        var path = Path.GetFullPath(Path.Combine(RootPath, conversationId));
        EnsureUnderRoot(path, RootPath, nameof(conversationId));
        return path;
    }

    public string GetConversationFile(
        string conversationId,
        AgentConversationStorageScope? scope = null)
        => Path.Combine(GetConversationDirectory(conversationId, scope), "conversation.json");

    public string GetSessionFile(
        string conversationId,
        AgentConversationStorageScope? scope = null)
        => Path.Combine(GetConversationDirectory(conversationId, scope), "session.json");

    string GetBusinessConversationDirectory(AgentConversationStorageScope scope)
    {
        if (!string.Equals(scope.BusinessType, "hotel", StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported agent business storage type.", nameof(scope));
        }

        ValidateSegment(scope.BusinessKey, 100, nameof(scope.BusinessKey));
        ValidateSegment(scope.OperationType, 64, nameof(scope.OperationType));
        ValidateSegment(scope.RunId, 64, nameof(scope.RunId));

        var path = Path.GetFullPath(Path.Combine(
            BusinessRootPath,
            "hotels",
            scope.BusinessKey,
            "operations",
            scope.OperationType,
            "runs",
            scope.RunId));
        EnsureUnderRoot(path, BusinessRootPath, nameof(scope));
        return path;
    }

    static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)
            || conversationId.Length > 128
            || conversationId.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '_' and not '-'))
        {
            throw new ArgumentException("Invalid conversation id.", nameof(conversationId));
        }
    }

    static void ValidateSegment(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '_' and not '-'))
        {
            throw new ArgumentException("Invalid business storage path segment.", parameterName);
        }
    }

    static void EnsureUnderRoot(string path, string root, string parameterName)
    {
        var expectedPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Storage path resolves outside the configured root.", parameterName);
        }
    }
}

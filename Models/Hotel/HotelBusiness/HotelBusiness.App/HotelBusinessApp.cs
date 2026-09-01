using AgentPlatform.Contracts;
using HotelBusiness.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HotelBusiness.App;

public interface IHotelBusinessService
{
    Task<IReadOnlyList<HotelBusinessRecentItem>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<HotelBusinessDetail?> GetAsync(string mtHotelCode, CancellationToken cancellationToken = default);

    Task<JsonElement?> GetResultAsync(
        string mtHotelCode,
        string operationType,
        int? resultVersion,
        CancellationToken cancellationToken = default);

    Task<HotelBusinessRunConversation?> GetRunConversationAsync(
        string mtHotelCode,
        string operationType,
        string runId,
        CancellationToken cancellationToken = default);
}

public interface IHotelBusinessStore
{
    Task<IReadOnlyList<HotelBusinessRecentItem>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<HotelBusinessDetail?> GetAsync(string mtHotelCode, CancellationToken cancellationToken = default);

    Task<HotelOperationRunHandle> StartRunAsync(
        HotelOperationRunStart start,
        CancellationToken cancellationToken = default);

    Task<T> CompleteRunAsync<T>(
        HotelOperationRunHandle run,
        HotelProcessorDescriptor processor,
        Func<int, DateTimeOffset, T> resultFactory,
        CancellationToken cancellationToken = default);

    Task FinishRunWithoutResultAsync(
        HotelOperationRunHandle run,
        HotelOperationRunStatus status,
        Exception? exception,
        CancellationToken cancellationToken = default);

    Task<JsonElement?> GetResultAsync(
        string mtHotelCode,
        string operationType,
        int? resultVersion,
        CancellationToken cancellationToken = default);

    Task<HotelBusinessRunConversation?> GetRunConversationAsync(
        string mtHotelCode,
        string operationType,
        string runId,
        CancellationToken cancellationToken = default);
}

public sealed record HotelOperationRunStart
{
    public required string MTHotelCode { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public required string OperationType { get; init; }

    public string? ConversationId { get; init; }

    public required HotelProcessorDescriptor Processor { get; init; }
}

public sealed record HotelOperationRunHandle
{
    public required string MTHotelCode { get; init; }

    public required string HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public required string OperationType { get; init; }

    public required string RunId { get; init; }

    public required string ConversationId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required AgentConversationStorageScope StorageScope { get; init; }
}

public interface IHotelOperationConcurrencyGuard
{
    HotelOperationLease? TryAcquire(string mtHotelCode, string operationType);
}

public sealed class HotelOperationLease(Action release) : IDisposable
{
    int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            release();
        }
    }
}

public sealed class HotelOperationAlreadyRunningException(string mtHotelCode, string operationType)
    : Exception($"Operation '{operationType}' is already running for MTHotelCode '{mtHotelCode}'.");

public sealed class HotelBusinessStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed partial class HotelBusinessService(IHotelBusinessStore store) : IHotelBusinessService
{
    public Task<IReadOnlyList<HotelBusinessRecentItem>> GetRecentAsync(
        CancellationToken cancellationToken = default)
        => store.GetRecentAsync(cancellationToken);

    public Task<HotelBusinessDetail?> GetAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default)
        => store.GetAsync(NormalizeHotelCode(mtHotelCode), cancellationToken);

    public Task<JsonElement?> GetResultAsync(
        string mtHotelCode,
        string operationType,
        int? resultVersion,
        CancellationToken cancellationToken = default)
    {
        if (resultVersion is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultVersion), "Result version must be positive.");
        }

        return store.GetResultAsync(
            NormalizeHotelCode(mtHotelCode),
            NormalizeSegment(operationType, 64, nameof(operationType)),
            resultVersion,
            cancellationToken);
    }

    public Task<HotelBusinessRunConversation?> GetRunConversationAsync(
        string mtHotelCode,
        string operationType,
        string runId,
        CancellationToken cancellationToken = default)
        => store.GetRunConversationAsync(
            NormalizeHotelCode(mtHotelCode),
            NormalizeSegment(operationType, 64, nameof(operationType)),
            NormalizeSegment(runId, 64, nameof(runId)),
            cancellationToken);

    public static string NormalizeHotelCode(string? mtHotelCode)
        => NormalizeSegment(mtHotelCode?.Trim().ToUpperInvariant(), 100, nameof(mtHotelCode));

    public static string NormalizeSegment(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || !SafeSegmentRegex().IsMatch(value))
        {
            throw new ArgumentException(
                $"{parameterName} must contain only ASCII letters, numbers, underscores, or hyphens.",
                parameterName);
        }

        return value;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSegmentRegex();
}

public sealed class HotelOperationConcurrencyGuard : IHotelOperationConcurrencyGuard
{
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public HotelOperationLease? TryAcquire(string mtHotelCode, string operationType)
    {
        var key = $"{mtHotelCode}:{operationType}";
        var gate = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        return gate.Wait(0) ? new HotelOperationLease(() => gate.Release()) : null;
    }
}

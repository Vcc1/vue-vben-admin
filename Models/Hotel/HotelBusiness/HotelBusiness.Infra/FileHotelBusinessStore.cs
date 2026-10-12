using AgentPlatform.Contracts;
using HotelBusiness.App;
using HotelBusiness.Contracts;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelBusiness.Infra;

internal sealed class FileHotelBusinessStore(HotelBusinessStoragePaths paths) : IHotelBusinessStore
{
    const int RecentHotelLimit = 20;

    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    readonly SemaphoreSlim _recentLock = new(1, 1);

    public async Task<IReadOnlyList<HotelBusinessRecentItem>> GetRecentAsync(
        CancellationToken cancellationToken = default)
    {
        var file = await ReadOptionalAsync<RecentHotelFile>(paths.RecentFile, cancellationToken);
        var hotels = file?.Hotels
            .OrderByDescending(item => item.UpdatedAt)
            .Take(RecentHotelLimit)
            .ToArray() ?? [];
        var results = new List<HotelBusinessRecentItem>(hotels.Length);
        foreach (var hotel in hotels)
        {
            var enriched = hotel;
            if (hotel.Operations.Count == 0)
            {
                var detail = await GetAsync(hotel.MTHotelCode, cancellationToken);
                if (detail is not null)
                {
                    enriched = enriched with
                    {
                        HotelNameEnglish = detail.HotelNameEnglish ?? enriched.HotelNameEnglish,
                        Operations = ToRecentOperations(detail.Operations)
                    };
                }
            }

            if (string.IsNullOrWhiteSpace(enriched.HotelNameEnglish)
                && enriched.LatestSuccessfulVersion is not null
                && enriched.LastOperationType == HotelBusinessOperations.HotelResearch)
            {
                var result = await GetResultAsync(
                    enriched.MTHotelCode,
                    HotelBusinessOperations.HotelResearch,
                    enriched.LatestSuccessfulVersion,
                    cancellationToken);
                var hotelNameEnglish = TryGetDatabaseHotelNameEnglish(result);
                if (!string.IsNullOrWhiteSpace(hotelNameEnglish))
                {
                    enriched = enriched with { HotelNameEnglish = hotelNameEnglish };
                }
            }

            results.Add(enriched);
        }

        return results;
    }

    public Task<HotelBusinessDetail?> GetAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default)
        => ReadOptionalAsync<HotelBusinessDetail>(paths.GetHotelFile(mtHotelCode), cancellationToken);

    public async Task<HotelOperationRunHandle> StartRunAsync(
        HotelOperationRunStart start,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(start.MTHotelCode, start.OperationType);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var runId = $"{now:yyyyMMdd'T'HHmmssfff'Z'}-{Guid.NewGuid():N}"[..33];
            var conversationId = string.IsNullOrWhiteSpace(start.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : start.ConversationId;
            var hotelName = string.IsNullOrWhiteSpace(start.HotelName)
                ? start.MTHotelCode
                : start.HotelName.Trim();
            var hotelNameEnglish = string.IsNullOrWhiteSpace(start.HotelNameEnglish)
                ? null
                : start.HotelNameEnglish.Trim();

            var run = new HotelBusinessRunSummary
            {
                MTHotelCode = start.MTHotelCode,
                OperationType = start.OperationType,
                RunId = runId,
                ConversationId = conversationId,
                Status = HotelOperationRunStatus.Running,
                StartedAt = now,
                Processor = start.Processor
            };

            var operation = await ReadOptionalAsync<HotelBusinessOperationSummary>(
                paths.GetOperationFile(start.MTHotelCode, start.OperationType),
                cancellationToken) ?? new HotelBusinessOperationSummary
                {
                    MTHotelCode = start.MTHotelCode,
                    OperationType = start.OperationType,
                    UpdatedAt = now
                };
            operation = operation with
            {
                UpdatedAt = now,
                Runs = [run, .. operation.Runs]
            };

            await WriteAtomicAsync(
                paths.GetRunFile(start.MTHotelCode, start.OperationType, runId),
                run,
                overwrite: true,
                cancellationToken);
            await WriteAtomicAsync(
                paths.GetOperationFile(start.MTHotelCode, start.OperationType),
                operation,
                overwrite: true,
                cancellationToken);
            var hotel = await UpdateHotelAsync(
                start.MTHotelCode,
                hotelName,
                hotelNameEnglish,
                operation,
                now,
                cancellationToken);
            await UpdateRecentAsync(hotel, operation, run, cancellationToken);

            return new HotelOperationRunHandle
            {
                MTHotelCode = start.MTHotelCode,
                HotelName = hotelName,
                HotelNameEnglish = hotelNameEnglish,
                OperationType = start.OperationType,
                RunId = runId,
                ConversationId = conversationId,
                StartedAt = now,
                StorageScope = new AgentConversationStorageScope
                {
                    BusinessType = "hotel",
                    BusinessKey = start.MTHotelCode,
                    OperationType = start.OperationType,
                    RunId = runId
                }
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> CompleteRunAsync<T>(
        HotelOperationRunHandle run,
        HotelProcessorDescriptor processor,
        Func<int, DateTimeOffset, T> resultFactory,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(run.MTHotelCode, run.OperationType);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var operation = await ReadRequiredAsync<HotelBusinessOperationSummary>(
                paths.GetOperationFile(run.MTHotelCode, run.OperationType),
                cancellationToken);
            var existingRun = operation.Runs.FirstOrDefault(item => item.RunId == run.RunId)
                ?? throw new HotelBusinessStorageException($"Run '{run.RunId}' does not exist.");
            var completedAt = DateTimeOffset.UtcNow;
            var resultVersion = (operation.LatestSuccessfulVersion ?? 0) + 1;
            var result = resultFactory(resultVersion, completedAt);
            var completedRun = existingRun with
            {
                Status = HotelOperationRunStatus.Completed,
                CompletedAt = completedAt,
                ResultVersion = resultVersion,
                Processor = processor,
                ErrorType = null,
                ErrorMessage = null
            };
            var version = new HotelBusinessResultVersionSummary
            {
                ResultVersion = resultVersion,
                RunId = run.RunId,
                ConversationId = run.ConversationId,
                CompletedAt = completedAt,
                Processor = processor
            };

            await WriteAtomicAsync(
                paths.GetResultFile(run.MTHotelCode, run.OperationType, resultVersion),
                result,
                overwrite: false,
                cancellationToken);
            await WriteAtomicAsync(
                paths.GetRunFile(run.MTHotelCode, run.OperationType, run.RunId),
                completedRun,
                overwrite: true,
                cancellationToken);

            operation = operation with
            {
                UpdatedAt = completedAt,
                LatestSuccessfulVersion = resultVersion,
                Versions = [version, .. operation.Versions],
                Runs = ReplaceRun(operation.Runs, completedRun)
            };
            await WriteAtomicAsync(
                paths.GetOperationFile(run.MTHotelCode, run.OperationType),
                operation,
                overwrite: true,
                cancellationToken);
            var hotel = await UpdateHotelAsync(
                run.MTHotelCode,
                run.HotelName,
                run.HotelNameEnglish,
                operation,
                completedAt,
                cancellationToken);
            await UpdateRecentAsync(hotel, operation, completedRun, cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task FinishRunWithoutResultAsync(
        HotelOperationRunHandle run,
        HotelOperationRunStatus status,
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (status is not (HotelOperationRunStatus.Failed or HotelOperationRunStatus.Cancelled))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var gate = GetLock(run.MTHotelCode, run.OperationType);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var operation = await ReadRequiredAsync<HotelBusinessOperationSummary>(
                paths.GetOperationFile(run.MTHotelCode, run.OperationType),
                cancellationToken);
            var existingRun = operation.Runs.FirstOrDefault(item => item.RunId == run.RunId)
                ?? throw new HotelBusinessStorageException($"Run '{run.RunId}' does not exist.");
            var completedAt = DateTimeOffset.UtcNow;
            var finishedRun = existingRun with
            {
                Status = status,
                CompletedAt = completedAt,
                ErrorType = exception?.GetType().Name,
                ErrorMessage = CreateErrorPreview(exception?.Message)
            };

            await WriteAtomicAsync(
                paths.GetRunFile(run.MTHotelCode, run.OperationType, run.RunId),
                finishedRun,
                overwrite: true,
                cancellationToken);
            operation = operation with
            {
                UpdatedAt = completedAt,
                Runs = ReplaceRun(operation.Runs, finishedRun)
            };
            await WriteAtomicAsync(
                paths.GetOperationFile(run.MTHotelCode, run.OperationType),
                operation,
                overwrite: true,
                cancellationToken);
            var hotel = await UpdateHotelAsync(
                run.MTHotelCode,
                run.HotelName,
                run.HotelNameEnglish,
                operation,
                completedAt,
                cancellationToken);
            await UpdateRecentAsync(hotel, operation, finishedRun, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<JsonElement?> GetResultAsync(
        string mtHotelCode,
        string operationType,
        int? resultVersion,
        CancellationToken cancellationToken = default)
    {
        var operation = await ReadOptionalAsync<HotelBusinessOperationSummary>(
            paths.GetOperationFile(mtHotelCode, operationType),
            cancellationToken);
        var version = resultVersion ?? operation?.LatestSuccessfulVersion;
        if (version is null)
        {
            return null;
        }

        var file = paths.GetResultFile(mtHotelCode, operationType, version.Value);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(file);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new HotelBusinessStorageException($"Unable to read result file '{file}'.", exception);
        }
    }

    public async Task<HotelBusinessRunConversation?> GetRunConversationAsync(
        string mtHotelCode,
        string operationType,
        string runId,
        CancellationToken cancellationToken = default)
    {
        var run = await ReadOptionalAsync<HotelBusinessRunSummary>(
            paths.GetRunFile(mtHotelCode, operationType, runId),
            cancellationToken);
        var conversation = await ReadOptionalAsync<AgentConversation>(
            paths.GetConversationFile(mtHotelCode, operationType, runId),
            cancellationToken);
        return run is null || conversation is null
            ? null
            : new HotelBusinessRunConversation { Run = run, Conversation = conversation };
    }

    async Task<HotelBusinessDetail> UpdateHotelAsync(
        string mtHotelCode,
        string hotelName,
        string? hotelNameEnglish,
        HotelBusinessOperationSummary operation,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var hotel = await ReadOptionalAsync<HotelBusinessDetail>(
            paths.GetHotelFile(mtHotelCode),
            cancellationToken) ?? new HotelBusinessDetail
            {
                MTHotelCode = mtHotelCode,
                HotelName = hotelName,
                HotelNameEnglish = hotelNameEnglish,
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt
            };
        hotel = hotel with
        {
            HotelName = hotelName,
            HotelNameEnglish = hotelNameEnglish ?? hotel.HotelNameEnglish,
            UpdatedAt = updatedAt,
            Operations = [
                operation,
                .. hotel.Operations.Where(item => item.OperationType != operation.OperationType)
            ]
        };
        await WriteAtomicAsync(paths.GetHotelFile(mtHotelCode), hotel, overwrite: true, cancellationToken);
        return hotel;
    }

    async Task UpdateRecentAsync(
        HotelBusinessDetail hotel,
        HotelBusinessOperationSummary operation,
        HotelBusinessRunSummary run,
        CancellationToken cancellationToken)
    {
        await _recentLock.WaitAsync(cancellationToken);
        try
        {
            var recent = await ReadOptionalAsync<RecentHotelFile>(paths.RecentFile, cancellationToken)
                ?? new RecentHotelFile();
            var item = new HotelBusinessRecentItem
            {
                MTHotelCode = hotel.MTHotelCode,
                HotelName = hotel.HotelName,
                HotelNameEnglish = hotel.HotelNameEnglish,
                UpdatedAt = hotel.UpdatedAt,
                LastOperationType = operation.OperationType,
                LastRunStatus = run.Status,
                LatestSuccessfulVersion = operation.LatestSuccessfulVersion,
                Operations = ToRecentOperations(hotel.Operations)
            };
            recent = recent with
            {
                Hotels = [
                    item,
                    .. recent.Hotels
                        .Where(existing => existing.MTHotelCode != hotel.MTHotelCode)
                        .OrderByDescending(existing => existing.UpdatedAt)
                        .Take(RecentHotelLimit - 1)
                ]
            };
            await WriteAtomicAsync(paths.RecentFile, recent, overwrite: true, cancellationToken);
        }
        finally
        {
            _recentLock.Release();
        }
    }

    SemaphoreSlim GetLock(string mtHotelCode, string operationType)
        => _locks.GetOrAdd($"{mtHotelCode}:{operationType}", static _ => new SemaphoreSlim(1, 1));

    static IReadOnlyList<HotelBusinessRunSummary> ReplaceRun(
        IReadOnlyList<HotelBusinessRunSummary> runs,
        HotelBusinessRunSummary replacement)
        => runs.Select(item => item.RunId == replacement.RunId ? replacement : item).ToArray();

    static string? CreateErrorPreview(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = string.Join(' ', message.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    static string? TryGetDatabaseHotelNameEnglish(JsonElement? result)
    {
        if (result is null || result.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var value = result.Value;
        if (!value.TryGetProperty("databaseHotel", out var databaseHotel)
            || !databaseHotel.TryGetProperty("hotelNameEnglish", out var hotelNameEnglish)
            || hotelNameEnglish.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return hotelNameEnglish.GetString()?.Trim();
    }

    static IReadOnlyList<HotelBusinessRecentOperation> ToRecentOperations(
        IReadOnlyList<HotelBusinessOperationSummary> operations)
        => operations
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new HotelBusinessRecentOperation
            {
                OperationType = item.OperationType,
                UpdatedAt = item.UpdatedAt,
                LastRunStatus = item.Runs.FirstOrDefault()?.Status
                    ?? HotelOperationRunStatus.Cancelled,
                LatestSuccessfulVersion = item.LatestSuccessfulVersion
            })
            .ToArray();

    static async Task<T?> ReadOptionalAsync<T>(string file, CancellationToken cancellationToken)
    {
        if (!File.Exists(file))
        {
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new HotelBusinessStorageException($"Unable to read hotel business file '{file}'.", exception);
        }
    }

    static async Task<T> ReadRequiredAsync<T>(string file, CancellationToken cancellationToken)
        => await ReadOptionalAsync<T>(file, cancellationToken)
            ?? throw new HotelBusinessStorageException($"Required hotel business file '{file}' does not exist.");

    static async Task WriteAtomicAsync<T>(
        string targetFile,
        T value,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetFile)!;
        var temporaryFile = Path.Combine(directory, $".{Path.GetFileName(targetFile)}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(
                temporaryFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
            }

            File.Move(temporaryFile, targetFile, overwrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new HotelBusinessStorageException($"Unable to write hotel business file '{targetFile}'.", exception);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
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

    sealed record RecentHotelFile
    {
        public int FileSchemaVersion { get; init; } = 1;

        public IReadOnlyList<HotelBusinessRecentItem> Hotels { get; init; } = [];
    }
}

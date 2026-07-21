using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.App;

public sealed class HotelCleaningService
{
    readonly IHotelCleaningRepository _repository;
    readonly HotelCleaner _cleaner;
    readonly IOptions<HotelCleaningOptions> _options;
    readonly IHotelStarLevelIssueRecorder _starLevelIssueRecorder;
    readonly ILogger<HotelCleaningService> _logger;

    public HotelCleaningService(
        IHotelCleaningRepository repository,
        HotelCleaner cleaner,
        IOptions<HotelCleaningOptions> options,
        IHotelStarLevelIssueRecorder starLevelIssueRecorder,
        ILogger<HotelCleaningService> logger)
    {
        _repository = repository;
        _cleaner = cleaner;
        _options = options;
        _starLevelIssueRecorder = starLevelIssueRecorder;
        _logger = logger;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var changedSince = DateTimeOffset.Now.AddHours(-options.ChangeWindowHours);
        var cleanedCount = 0;
        var progressByType = new Dictionary<HotelDataType, HotelCleaningProgress>();
        foreach (var sourceType in new[] { HotelDataType.Platform, HotelDataType.Supplier })
        {
            var progress = await _repository.GetCleaningProgressAsync(
                sourceType,
                changedSince,
                options.Version,
                options.RegionFenceVersion,
                cancellationToken);
            progressByType[sourceType] = progress;
            LogProgress(sourceType, progress, batchSaved: 0, runSaved: cleanedCount);
        }

        foreach (var type in new[] { HotelDataType.Platform, HotelDataType.Supplier })
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var rawHotels = await _repository.GetHotelsToCleanAsync(
                    type,
                    changedSince,
                    options.Version,
                    options.RegionFenceVersion,
                    options.BatchSize,
                    cancellationToken);
                if (rawHotels.Count == 0)
                {
                    break;
                }

                var results = new List<HotelCleaningResult>(rawHotels.Count);
                var failures = new List<HotelCleaningFailureRecord>();
                foreach (var raw in rawHotels)
                {
                    if (TryClean(raw, options.Version, options.RegionFenceVersion, cancellationToken, out var result, out var failure))
                    {
                        results.Add(result!);
                    }
                    else
                    {
                        failures.Add(failure!);
                    }
                }

                await _starLevelIssueRecorder.FlushAsync(cancellationToken);
                await _repository.RecordCleaningFailuresAsync(failures, cancellationToken);
                var savedCount = 0;
                if (results.Count > 0)
                {
                    foreach (var saveBatch in results.Chunk(options.SaveBatchSize))
                    {
                        savedCount += await _repository.SaveCleanedHotelsAsync(saveBatch, cancellationToken);
                    }
                }
                cleanedCount += savedCount;
                var previousProgress = progressByType[type];
                var newFailureCount = failures.Count + results.Count - savedCount;
                var progress = previousProgress with
                {
                    Cleaned = Math.Min(previousProgress.Total, previousProgress.Cleaned + savedCount),
                    Failed = Math.Min(previousProgress.Total, previousProgress.Failed + newFailureCount)
                };
                progressByType[type] = progress;
                _logger.LogInformation(
                    "Hotel cleaning batch completed. Type={Type}, SavedCount={SavedCount}, FailureCount={FailureCount}, RunSavedCount={RunSavedCount}, CleanVersion={CleanVersion}, RegionBoundaryVersion={RegionBoundaryVersion}",
                    type,
                    savedCount,
                    newFailureCount,
                    cleanedCount,
                    options.Version,
                    options.RegionFenceVersion);
                LogProgress(type, progress, savedCount, cleanedCount);
            }
        }

        return cleanedCount;
    }

    void LogProgress(HotelDataType type, HotelCleaningProgress progress, int batchSaved, int runSaved)
    {
        _logger.LogInformation(
            "Hotel cleaning progress. Source={Source}, Total={Total}, Cleaned={Cleaned}, Remaining={Remaining}, Failed={Failed}, Pending={Pending}, BatchSaved={BatchSaved}, RunSaved={RunSaved}",
            type == HotelDataType.Platform ? "OTA" : "SUP",
            progress.Total,
            progress.Cleaned,
            progress.Remaining,
            progress.Failed,
            progress.Pending,
            batchSaved,
            runSaved);
    }

    bool TryClean(
        RawHotelData raw,
        int cleanVersion,
        int regionBoundaryVersion,
        CancellationToken cancellationToken,
        out HotelCleaningResult? result,
        out HotelCleaningFailureRecord? failure)
    {
        try
        {
            result = _cleaner.Clean(raw, cleanVersion, cancellationToken);
            failure = null;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hotel cleaning failed. Type={Type}, HotelCode={HotelCode}, SupId={SupId}", raw.Type, raw.HotelCode, raw.SupplierId);
            result = null;
            failure = new HotelCleaningFailureRecord(
                raw.Type,
                raw.Id ?? raw.PlatformHotelId ?? 0,
                raw.SupplierId,
                raw.SourceSystemId,
                raw.HotelCode,
                ex.GetType().FullName ?? ex.GetType().Name,
                ex.Message,
                ex.ToString(),
                cleanVersion,
                regionBoundaryVersion);
            return false;
        }
    }
}

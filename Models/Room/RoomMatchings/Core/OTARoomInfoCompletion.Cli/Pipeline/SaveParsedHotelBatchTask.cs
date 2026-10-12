using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class SaveParsedHotelBatchTask
{
    private const string StageName = "save_parsed_hotels";
    private const string InputQueueName = "parsed_hotels";
    private const int MaxSqlParametersPerSave = 1_800;
    private const int InsertOperationParameterCost = 20;
    private const int UpdateOperationParameterCost = 21;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SaveParsedHotelBatchTask> _logger;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public SaveParsedHotelBatchTask(IServiceScopeFactory scopeFactory, ILogger<SaveParsedHotelBatchTask> logger, OtaRoomInfoCompletionOptions options, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> parsedHotelReader, CancellationToken cancellationToken)
    {
        var batch = new List<OtaRoomInfoCompletionWorkItem>(_options.SaveBatchSize);

        await foreach (var item in parsedHotelReader.ReadAllAsync(cancellationToken))
        {
            _metrics.DecreaseQueueDepth(InputQueueName);
            batch.Add(item);
            if (batch.Count < _options.SaveBatchSize)
            {
                continue;
            }

            await SaveBatchWithMetricsAsync(batch, cancellationToken);
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await SaveBatchWithMetricsAsync(batch, cancellationToken);
        }
    }

    private async Task SaveBatchWithMetricsAsync(List<OtaRoomInfoCompletionWorkItem> batch, CancellationToken cancellationToken)
    {
        _metrics.IncreaseRunningTasks(StageName);
        try
        {
            await SaveBatchAsync(batch, cancellationToken);
            _metrics.RecordCompletedTasks(StageName);
        }
        catch (Exception ex)
        {
            _metrics.RecordErrors(StageName);
            _logger.LogError(ex, "入库阶段批处理异常已抑制, Stage: {StageName}, BatchSize: {BatchSize}", StageName, batch.Count);
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }

    private async Task SaveBatchAsync(List<OtaRoomInfoCompletionWorkItem> batch, CancellationToken cancellationToken)
    {
        var extensions = batch
            .SelectMany(x => x.OTARoomInfoExtensions)
            .GroupBy(x => x.Id)
            .Select(x => x.Last())
            .Where(HasMeaningfulData)
            .ToList();

        if (extensions.Count == 0)
        {
            _logger.LogInformation("入库阶段无扩展数据变更, BatchSize: {BatchSize}", batch.Count);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        var existingExtensions = batch
            .SelectMany(x => x.OtaRooms)
            .Where(x => x.Extension != null)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.Last().Extension!);

        var insertedCount = 0;
        var updatedCount = 0;
        var pendingParameterCount = 0;

        foreach (var extension in extensions)
        {
            if (!existingExtensions.TryGetValue(extension.Id, out var existingExtension))
            {
                if (pendingParameterCount + InsertOperationParameterCost > MaxSqlParametersPerSave)
                {
                    await FlushPendingChangesAsync(dbContext, cancellationToken);
                    pendingParameterCount = 0;
                }

                Normalize(extension);
                dbContext.OTARoomInfoExtensions.Add(extension);
                dbContext.OTARoomInfoExtensionLogs.Add(CreateLog(extension, OTARoomInfoExtensionLogType.Insert));
                pendingParameterCount += InsertOperationParameterCost;
                insertedCount++;
                continue;
            }

            if (!HasChanges(existingExtension, extension))
            {
                continue;
            }

            if (pendingParameterCount + UpdateOperationParameterCost > MaxSqlParametersPerSave)
            {
                await FlushPendingChangesAsync(dbContext, cancellationToken);
                pendingParameterCount = 0;
            }

            dbContext.OTARoomInfoExtensions.Attach(existingExtension);
            ApplyValues(existingExtension, extension);
            dbContext.OTARoomInfoExtensionLogs.Add(CreateLog(existingExtension, OTARoomInfoExtensionLogType.Update));
            pendingParameterCount += UpdateOperationParameterCost;
            updatedCount++;
        }

        if (insertedCount == 0 && updatedCount == 0)
        {
            _logger.LogInformation("入库阶段扩展数据无变化, BatchSize: {BatchSize}, ExtensionCount: {ExtensionCount}", batch.Count, extensions.Count);
            return;
        }

        await FlushPendingChangesAsync(dbContext, cancellationToken);
        _logger.LogInformation("入库阶段扩展数据写入完成, BatchSize: {BatchSize}, Inserted: {InsertedCount}, Updated: {UpdatedCount}", batch.Count, insertedCount, updatedCount);
    }

    private static async Task FlushPendingChangesAsync(RoomMappingDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.ChangeTracker.HasChanges())
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private static bool HasMeaningfulData(OTARoomInfoExtension extension)
    {
        return !string.IsNullOrWhiteSpace(extension.InternalView)
               || !string.IsNullOrWhiteSpace(extension.ExternalView)
               || !string.IsNullOrWhiteSpace(extension.InternalBedInfo)
               || !string.IsNullOrWhiteSpace(extension.ExternalBedInfo)
               || !string.IsNullOrWhiteSpace(extension.InternalArea)
               || !string.IsNullOrWhiteSpace(extension.ExternalArea)
               || !string.IsNullOrWhiteSpace(extension.Source);
    }

    private static bool HasChanges(OTARoomInfoExtension target, OTARoomInfoExtension source)
    {
        return IsChanged(target.MTHotelCode, source.MTHotelCode)
               || IsChanged(target.InternalView, source.InternalView)
               || IsChanged(target.ExternalView, source.ExternalView)
               || IsChanged(target.InternalBedInfo, source.InternalBedInfo)
               || IsChanged(target.ExternalBedInfo, source.ExternalBedInfo)
               || IsChanged(target.InternalArea, source.InternalArea)
               || IsChanged(target.ExternalArea, source.ExternalArea)
               || IsChanged(target.Source, source.Source);
    }

    private static void ApplyValues(OTARoomInfoExtension target, OTARoomInfoExtension source)
    {
        target.MTHotelCode = NormalizeValue(source.MTHotelCode);
        target.InternalView = NormalizeValue(source.InternalView);
        target.ExternalView = NormalizeValue(source.ExternalView);
        target.InternalBedInfo = NormalizeValue(source.InternalBedInfo);
        target.ExternalBedInfo = NormalizeValue(source.ExternalBedInfo);
        target.InternalArea = NormalizeValue(source.InternalArea);
        target.ExternalArea = NormalizeValue(source.ExternalArea);
        target.Source = NormalizeValue(source.Source);
    }

    private static void Normalize(OTARoomInfoExtension extension)
    {
        extension.MTHotelCode = NormalizeValue(extension.MTHotelCode);
        extension.InternalView = NormalizeValue(extension.InternalView);
        extension.ExternalView = NormalizeValue(extension.ExternalView);
        extension.InternalBedInfo = NormalizeValue(extension.InternalBedInfo);
        extension.ExternalBedInfo = NormalizeValue(extension.ExternalBedInfo);
        extension.InternalArea = NormalizeValue(extension.InternalArea);
        extension.ExternalArea = NormalizeValue(extension.ExternalArea);
        extension.Source = NormalizeValue(extension.Source);
    }

    private static bool IsChanged(string? oldValue, string? newValue)
    {
        return !string.Equals(NormalizeValue(oldValue), NormalizeValue(newValue), StringComparison.Ordinal);
    }

    private static OTARoomInfoExtensionLog CreateLog(OTARoomInfoExtension extension, OTARoomInfoExtensionLogType changeType)
    {
        return new OTARoomInfoExtensionLog
        {
            OTARoomInfoExtensionId = extension.Id,
            MTHotelCode = NormalizeValue(extension.MTHotelCode),
            ChangeType = changeType,
            InternalView = NormalizeValue(extension.InternalView),
            ExternalView = NormalizeValue(extension.ExternalView),
            InternalBedInfo = NormalizeValue(extension.InternalBedInfo),
            ExternalBedInfo = NormalizeValue(extension.ExternalBedInfo),
            InternalArea = NormalizeValue(extension.InternalArea),
            ExternalArea = NormalizeValue(extension.ExternalArea),
            Source = NormalizeValue(extension.Source),
            CreateTime = DateTime.Now
        };
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

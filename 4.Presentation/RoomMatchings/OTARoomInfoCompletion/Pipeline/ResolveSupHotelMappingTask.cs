using Infrastructure.Sql.MengTuAPI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class ResolveSupHotelMappingTask
{
    private const int EanSupId = 51;
    private const string StageName = "resolve_sup_hotel_mappings";
    private const string InputQueueName = "hotel_codes";
    private const string OutputQueueName = "resolved_sup_hotels";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResolveSupHotelMappingTask> _logger;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public ResolveSupHotelMappingTask(
        IServiceScopeFactory scopeFactory,
        ILogger<ResolveSupHotelMappingTask> logger,
        OtaRoomInfoCompletionOptions options,
        OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<string> hotelCodeReader, ChannelWriter<OtaRoomInfoCompletionWorkItem> resolvedHotelWriter, CancellationToken cancellationToken)
    {
        var batch = new List<string>(_options.EanBatchSize);

        try
        {
            await foreach (var hotelCode in hotelCodeReader.ReadAllAsync(cancellationToken))
            {
                _metrics.DecreaseQueueDepth(InputQueueName);
                batch.Add(hotelCode);
                if (batch.Count < _options.EanBatchSize)
                {
                    continue;
                }

                await ResolveBatchWithMetricsAsync(batch, resolvedHotelWriter, cancellationToken);
                batch = new List<string>(_options.EanBatchSize);
            }

            if (batch.Count > 0)
            {
                await ResolveBatchWithMetricsAsync(batch, resolvedHotelWriter, cancellationToken);
            }

            resolvedHotelWriter.TryComplete();
        }
        catch (Exception ex)
        {
            resolvedHotelWriter.TryComplete(ex);
            throw;
        }
    }

    private async Task ResolveBatchWithMetricsAsync(List<string> batch, ChannelWriter<OtaRoomInfoCompletionWorkItem> resolvedHotelWriter, CancellationToken cancellationToken)
    {
        _metrics.IncreaseRunningTasks(StageName);
        try
        {
            await ResolveBatchAsync(batch, resolvedHotelWriter, cancellationToken);
            _metrics.RecordCompletedTasks(StageName);
        }
        catch
        {
            _metrics.RecordErrors(StageName);
            throw;
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }

    private async Task ResolveBatchAsync(List<string> batch, ChannelWriter<OtaRoomInfoCompletionWorkItem> resolvedHotelWriter, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mengTuApiContext = scope.ServiceProvider.GetRequiredService<MengTuAPIContext>();

        var hotelMappings = await mengTuApiContext.SupHotelInfos
            .AsNoTracking()
            .Where(x => x.SupId == EanSupId && batch.Contains(x.MTHotelCode) && !string.IsNullOrWhiteSpace(x.SHotelCode))
            .Select(x => new { x.MTHotelCode, x.SHotelCode, x.SupId })
            .ToListAsync(cancellationToken);

        var mappingByMtHotelCode = hotelMappings
            .Where(x => !string.IsNullOrWhiteSpace(x.MTHotelCode))
            .GroupBy(x => x.MTHotelCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var mtHotelCode in batch)
        {
            if (!mappingByMtHotelCode.TryGetValue(mtHotelCode, out var hotelMapping))
            {
                _logger.LogWarning("未找到 SupHotelInfo 映射, MTHotelCode: {MTHotelCode}, SupId: {SupId}", mtHotelCode, EanSupId);
                continue;
            }

            await resolvedHotelWriter.WriteAsync(new OtaRoomInfoCompletionWorkItem
            {
                MTHotelCode = mtHotelCode,
                SHotelCode = hotelMapping.SHotelCode.Trim(),
                SupId = hotelMapping.SupId,
            }, cancellationToken);
            _metrics.IncreaseQueueDepth(OutputQueueName);
        }
    }
}

using OTAHotelInfoSync.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncService : IOTAHotelInfoSyncService
{
    const int InitialMinHotelId = 0;
    const int RemotePageSize = 10_000;
    readonly IOTAHotelInfoRepository _repository;
    readonly IOTAHotelInfoSource _source;
    readonly IOptionsMonitor<OTAHotelInfoSyncOptions> _options;
    readonly OTAHotelInfoSyncRetryExecutor _retryExecutor;
    readonly ILogger<OTAHotelInfoSyncService> _logger;

    public OTAHotelInfoSyncService(
        IOTAHotelInfoRepository repository,
        IOTAHotelInfoSource source,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options,
        OTAHotelInfoSyncRetryExecutor retryExecutor,
        ILogger<OTAHotelInfoSyncService> logger)
    {
        _repository = repository;
        _source = source;
        _options = options;
        _retryExecutor = retryExecutor;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("OTA hotel information synchronization is disabled.");
            return;
        }

        var hasRows = await ExecuteDatabaseAsync(
            "检查本地酒店数据",
            _repository.HasAnyAsync,
            cancellationToken);
        if (!hasRows)
        {
            await RunInitialSyncAsync(cancellationToken);
            return;
        }

        var lastModifyTime = await ExecuteDatabaseAsync(
            "读取最后更新时间",
            _repository.GetLastModifyTimeAsync,
            cancellationToken);
        if (!lastModifyTime.HasValue)
        {
            _logger.LogWarning("OTAHotelInfoRaw has no usable LastModifyTime. Running the initial ID synchronization.");
            await RunInitialSyncAsync(cancellationToken);
            return;
        }

        var startTime = lastModifyTime.Value.AddMinutes(-2);
        _logger.LogInformation("Starting OTA hotel incremental synchronization. StartTime={StartTime}", startTime);

        var hotels = await ExecuteNetworkAsync(
            "按时间获取增量酒店",
            token => _source.QueryByTimeAsync(startTime, null, token),
            cancellationToken);
        var upserted = await ExecuteDatabaseAsync(
            "写入增量酒店",
            token => _repository.UpsertAsync(hotels, token),
            cancellationToken);

        _logger.LogInformation(
            "OTA hotel incremental synchronization completed. Received={Received}, Upserted={Upserted}, StartTime={StartTime}",
            hotels.Count,
            upserted,
            startTime);
    }

    async Task RunInitialSyncAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting OTA hotel initial synchronization. MinHotelId={MinHotelId}, PageSize={PageSize}, DateTime={DateTime}",
            InitialMinHotelId,
            RemotePageSize,
            new DateTime(1970, 1, 1));

        var minHotelId = InitialMinHotelId;
        var totalUpserted = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var hotels = await ExecuteNetworkAsync(
                $"按酒店 ID 获取全量批次（MinHotelId={minHotelId}）",
                token => _source.QueryByMinHotelIdAsync(
                    minHotelId,
                    RemotePageSize,
                    token),
                cancellationToken);
            if (hotels.Count == 0)
            {
                break;
            }

            totalUpserted += await ExecuteDatabaseAsync(
                $"写入全量酒店批次（MinHotelId={minHotelId}）",
                token => _repository.UpsertAsync(hotels, token),
                cancellationToken);
            var nextMinHotelId = hotels.Max(e => e.HotelID);
            if (nextMinHotelId <= minHotelId)
            {
                throw new InvalidOperationException(
                    $"OTA hotel initial synchronization did not advance the hotel ID cursor. Current={minHotelId}, Next={nextMinHotelId}.");
            }

            _logger.LogInformation(
                "OTA hotel initial synchronization batch completed. MinHotelId={MinHotelId}, NextMinHotelId={NextMinHotelId}, Received={Received}, TotalUpserted={TotalUpserted}",
                minHotelId,
                nextMinHotelId,
                hotels.Count,
                totalUpserted);
            minHotelId = nextMinHotelId;
        }

        _logger.LogInformation("OTA hotel initial synchronization completed. TotalUpserted={TotalUpserted}", totalUpserted);
    }

    Task<T> ExecuteNetworkAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        return _retryExecutor.ExecuteAsync("网络请求", operation, action, cancellationToken);
    }

    Task<T> ExecuteDatabaseAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        return _retryExecutor.ExecuteAsync("数据库操作", operation, action, cancellationToken);
    }

}

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
    readonly ILogger<OTAHotelInfoSyncService> _logger;

    public OTAHotelInfoSyncService(
        IOTAHotelInfoRepository repository,
        IOTAHotelInfoSource source,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options,
        ILogger<OTAHotelInfoSyncService> logger)
    {
        _repository = repository;
        _source = source;
        _options = options;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("OTA hotel information synchronization is disabled.");
            return;
        }

        var hasRows = await _repository.HasAnyAsync(cancellationToken);
        if (!hasRows)
        {
            await RunInitialSyncAsync(cancellationToken);
            return;
        }

        var lastModifyTime = await _repository.GetLastModifyTimeAsync(cancellationToken);
        if (!lastModifyTime.HasValue)
        {
            _logger.LogWarning("OTAHotelInfoRaw has no usable LastModifyTime. Running the initial ID synchronization.");
            await RunInitialSyncAsync(cancellationToken);
            return;
        }

        var startTime = lastModifyTime.Value.AddMinutes(-2);
        _logger.LogInformation("Starting OTA hotel incremental synchronization. StartTime={StartTime}", startTime);

        var hotels = await _source.QueryByTimeAsync(startTime, null, cancellationToken);
        var upserted = await _repository.UpsertAsync(hotels, cancellationToken);

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
            var hotels = await _source.QueryByMinHotelIdAsync(
                minHotelId,
                RemotePageSize,
                cancellationToken);
            if (hotels.Count == 0)
            {
                break;
            }

            totalUpserted += await _repository.UpsertAsync(hotels, cancellationToken);
            var nextMinHotelId = hotels[^1].HotelID;
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

}

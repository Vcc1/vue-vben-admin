using Infrastructure.Sql.MengTuSup.Entitys;
using Microsoft.Extensions.Logging;
using OTARoomBuilder;
using OTARoomBuilder.Contracts;
using RoomMatch.Core.RoomMatchingStrategies;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;

namespace Infrastructure.OTARoomBuilder;

public sealed class EanSupplierRoomBuildPlugin : BaseSupplierRoomBuildPlugin<FullSequenceMatcher>
{
    readonly ISupplierStaticDataService _supplierStaticDataService;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<EanSupplierRoomBuildPlugin> _logger;

    public EanSupplierRoomBuildPlugin(
        ISupplierStaticDataService supplierStaticDataService,
        OTARoomBuilderMetrics metrics,
        ILogger<EanSupplierRoomBuildPlugin> logger)
    {
        _supplierStaticDataService = supplierStaticDataService;
        _metrics = metrics;
        _logger = logger;
    }

    public override int SupId => 51;

    public override int Priority => 1;

    public override bool CanEnrich(SupRoomInfo room)
    {
        return !string.IsNullOrWhiteSpace(room.SHotelCode) && !string.IsNullOrWhiteSpace(room.SRoomCode);
    }

    public override async Task<SupplierRoomEnrichmentResult> EnrichAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken)
    {
        if (!CanEnrich(context.SupplierRoom))
        {
            return SupplierRoomEnrichmentResult.NotSupported;
        }

        SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> result;
        try
        {
            result = await LoadHotelAndRoomsAsync(context, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordStaticDataRoomMatch(SupId, "lookup_failed");
            _logger.LogWarning(
                ex,
                "Failed to load supplier static data because provider request failed. SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                SupId,
                context.MTHotelCode,
                context.SHotelCode,
                context.SRoomCode);
            return SupplierRoomEnrichmentResult.NotSupported;
        }

        if (!result.Success || result.Data is null)
        {
            _logger.LogWarning(
                "Failed to load supplier static data. MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}, ErrorCode={ErrorCode}",
                context.MTHotelCode,
                context.SHotelCode,
                context.SRoomCode,
                result.ErrorCode);
            return SupplierRoomEnrichmentResult.NotSupported;
        }

        var room = result.Data.Rooms.FirstOrDefault(item =>
            string.Equals(item.RoomCode, context.SRoomCode, StringComparison.OrdinalIgnoreCase));
        if (room is null)
        {
            if (result.Data.Rooms.Count > 0)
            {
                _metrics.RecordStaticDataRoomMatch(SupId, "room_code_not_found");
            }

            _logger.LogWarning(
                "Supplier static data room not found. MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                context.MTHotelCode,
                context.SHotelCode,
                context.SRoomCode);
            return SupplierRoomEnrichmentResult.NotSupported;
        }

        var maxAdt = room.MaxOccupancy ?? 0;
        if (maxAdt <= 0)
        {
            _metrics.RecordStaticDataRoomMatch(SupId, "max_occupancy_missing");
            _logger.LogWarning(
                "Supplier static data max occupancy missing. SupId={SupId}, SupplierRoomId={SupplierRoomId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                SupId,
                context.SupplierRoom.Id,
                context.MTHotelCode,
                context.SHotelCode,
                context.SRoomCode);
            return SupplierRoomEnrichmentResult.NotSupported;
        }

        _metrics.RecordStaticDataRoomMatch(SupId, "matched");

        return new SupplierRoomEnrichmentResult
        {
            Success = true,
            MaxAdt = maxAdt,
            DataKind = global::OTARoomBuilder.SupplierStaticDataKind.HotelAndRoom,
            CacheHotelCode = context.SHotelCode,
            CacheRoomCode = context.SRoomCode,
            PayloadFormat = "json"
        };
    }

    async Task<SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> LoadHotelAndRoomsAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildStaticDataCacheKey(context);
        if (context.Items is not null
            && context.Items.TryGetValue(cacheKey, out var cached)
            && cached is SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> cachedResult)
        {
            return cachedResult;
        }

        var result = await _supplierStaticDataService.GetHotelAndRoomsBySupplierHotelCodeAsync(
            SupId,
            context.SHotelCode!,
            new SupplierStaticDataRequestOptions
            {
                ForceRefresh = false,
                MTHotelCode = context.MTHotelCode
            },
            cancellationToken);

        context.Items?.TryAdd(cacheKey, result);
        return result;
    }

    static string BuildStaticDataCacheKey(SupplierRoomBuildContext context)
    {
        return string.Join(
            '|',
            "supplier-static-data",
            context.SupplierRoom.SupId,
            context.SHotelCode?.Trim() ?? string.Empty,
            context.MTHotelCode?.Trim() ?? string.Empty);
    }
}

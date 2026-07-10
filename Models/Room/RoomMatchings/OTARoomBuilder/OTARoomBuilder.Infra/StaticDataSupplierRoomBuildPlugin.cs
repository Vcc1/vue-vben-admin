using Infrastructure.Sql.MengTuSup.Entitys;
using Microsoft.Extensions.Logging;
using OTARoomBuilder;
using RoomMatch.Core.RoomMatchingStrategies;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;

namespace Infrastructure.OTARoomBuilder;

public sealed class StaticDataSupplierRoomBuildPlugin : BaseSupplierRoomBuildPlugin<StrictEqualMatcher>
{
    readonly int _supId;
    readonly int _priority;
    readonly ISupplierStaticDataService _supplierStaticDataService;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<StaticDataSupplierRoomBuildPlugin> _logger;

    public StaticDataSupplierRoomBuildPlugin(
        int supId,
        int priority,
        ISupplierStaticDataService supplierStaticDataService,
        OTARoomBuilderMetrics metrics,
        ILogger<StaticDataSupplierRoomBuildPlugin> logger)
    {
        _supId = supId;
        _priority = priority;
        _supplierStaticDataService = supplierStaticDataService;
        _metrics = metrics;
        _logger = logger;
    }

    public override int SupId => _supId;

    public override int Priority => _priority;

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

        var result = await LoadHotelAndRoomsAsync(context, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            _logger.LogWarning(
                "Failed to load supplier static data for MaxAdt enrichment. SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}, ErrorCode={ErrorCode}",
                SupId,
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
                "Supplier static room not found for MaxAdt enrichment. SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                SupId,
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
                "Supplier static room max occupancy missing for MaxAdt enrichment. SupId={SupId}, SupplierRoomId={SupplierRoomId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
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

    string BuildStaticDataCacheKey(SupplierRoomBuildContext context)
    {
        return string.Join(
            '|',
            "supplier-static-data",
            SupId,
            context.SHotelCode?.Trim() ?? string.Empty,
            context.MTHotelCode?.Trim() ?? string.Empty);
    }
}

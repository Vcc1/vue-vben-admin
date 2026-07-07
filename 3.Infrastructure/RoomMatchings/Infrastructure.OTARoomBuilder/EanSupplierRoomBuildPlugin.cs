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
    readonly ILogger<EanSupplierRoomBuildPlugin> _logger;

    public EanSupplierRoomBuildPlugin(
        ISupplierStaticDataService supplierStaticDataService,
        ILogger<EanSupplierRoomBuildPlugin> logger)
    {
        _supplierStaticDataService = supplierStaticDataService;
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

        var result = await _supplierStaticDataService.GetHotelAndRoomsBySupplierHotelCodeAsync(
            SupId,
            context.SHotelCode!,
            new SupplierStaticDataRequestOptions
            {
                ForceRefresh = false
            },
            cancellationToken);

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
            _logger.LogWarning(
                "Supplier static data max occupancy missing. SupId={SupId}, SupplierRoomId={SupplierRoomId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                SupId,
                context.SupplierRoom.Id,
                context.MTHotelCode,
                context.SHotelCode,
                context.SRoomCode);
            return SupplierRoomEnrichmentResult.NotSupported;
        }

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
}

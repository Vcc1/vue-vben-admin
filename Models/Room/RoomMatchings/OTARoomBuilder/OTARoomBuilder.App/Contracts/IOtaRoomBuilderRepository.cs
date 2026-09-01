using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;

namespace OTARoomBuilder.Contracts;

public interface IOtaRoomBuilderRepository
{
    Task<OtaRoomBuilderIncrementalBatch> LoadIncrementalMtHotelCodesAsync(
        OtaRoomBuilderIncrementalCursor? cursor,
        int batchSize,
        IReadOnlyCollection<int> enabledSupIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> LoadMtHotelCodesAsync(
        int lastDaysOffset,
        IReadOnlyCollection<int> enabledSupIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupRoomInfo>> LoadPreferredSupplierRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        IReadOnlyCollection<int> enabledSupIds,
        int? lastDaysOffset,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupRoomInfo>> LoadAiSupplierRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        int? lastDaysOffset,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OTARoomInfo>> LoadOtaRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        CancellationToken cancellationToken);

    Task BulkInsertOtaRoomInfoPresAsync(
        IReadOnlyList<OTARoomInfoPre> rooms,
        CancellationToken cancellationToken);
}

public sealed record OtaRoomBuilderIncrementalCursor(DateTime LastUpdateDate, int LastId);

public sealed record OtaRoomBuilderIncrementalBatch(
    IReadOnlyList<string> HotelCodes,
    OtaRoomBuilderIncrementalCursor? NextCursor);

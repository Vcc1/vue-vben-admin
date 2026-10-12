namespace SupplierStaticDataApp.Contracts;

public interface ISupplierStaticDataService
{
    Task<SupplierStaticDataResult<SupplierStaticHotelDto>> GetHotelBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>> GetRoomsBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> GetHotelAndRoomsBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelDto>>> GetHotelsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>>> GetRoomsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<SupplierStaticDataResult<SupplierStaticHotelDto>> GetHotelByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>> GetRoomsByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> GetHotelAndRoomsByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelDto>>> GetHotelsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>>> GetRoomsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);
}

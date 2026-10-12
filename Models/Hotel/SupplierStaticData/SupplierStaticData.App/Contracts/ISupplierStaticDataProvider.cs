namespace SupplierStaticDataApp.Contracts;

public interface ISupplierStaticDataProvider
{
    int SupId { get; }

    bool Supports(SupplierStaticDataKind dataKind);

    Task<SupplierStaticDataFetchResult> FetchHotelAndRoomsEnvelopesAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken);

    SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> ParseHotelAndRooms(
        string supplierHotelCode,
        SupplierStaticDataEnvelope envelope);
}

namespace SupplierStaticDataApp.Contracts;

public interface ISupplierStaticDataCacheRepository
{
    Task<IReadOnlyDictionary<string, SupplierStaticDataEnvelope>> GetHotelAndRoomsAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SupplierStaticDataEnvelope>> GetAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataKind dataKind,
        CancellationToken cancellationToken);

    Task SaveAsync(
        SupplierStaticDataCacheLookup lookup,
        SupplierStaticDataEnvelope envelope,
        CancellationToken cancellationToken);
}

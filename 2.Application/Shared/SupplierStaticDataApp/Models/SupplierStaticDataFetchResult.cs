namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataFetchResult
{
    public IReadOnlyDictionary<string, IReadOnlyList<SupplierStaticDataEnvelope>> Envelopes { get; init; }
        = new Dictionary<string, IReadOnlyList<SupplierStaticDataEnvelope>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SupplierStaticDataRawInfo> RawInfos { get; init; }
        = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
}

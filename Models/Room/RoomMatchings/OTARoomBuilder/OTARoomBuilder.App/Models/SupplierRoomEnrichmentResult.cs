namespace OTARoomBuilder;

public sealed class SupplierRoomEnrichmentResult
{
    public static SupplierRoomEnrichmentResult NotSupported { get; } = new()
    {
        Success = false
    };

    public bool Success { get; init; }

    public int? MaxAdt { get; init; }

    public string? RawPayload { get; init; }

    public SupplierStaticDataKind? DataKind { get; init; }

    public string? CacheHotelCode { get; init; }

    public string? CacheRoomCode { get; init; }

    public string? PayloadFormat { get; init; }
}

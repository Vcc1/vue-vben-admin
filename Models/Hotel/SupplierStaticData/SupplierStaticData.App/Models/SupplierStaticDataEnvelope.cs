namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataEnvelope
{
    public int SupId { get; set; }

    public SupplierStaticDataKind DataKind { get; set; }

    public string HotelCode { get; set; } = string.Empty;

    public string? RoomCode { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public string PayloadFormat { get; set; } = "json";

    public string Payload { get; set; } = string.Empty;
}

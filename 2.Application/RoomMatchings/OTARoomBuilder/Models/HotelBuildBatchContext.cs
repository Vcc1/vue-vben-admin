namespace OTARoomBuilder;

public sealed class HotelBuildBatchContext
{
    public required IReadOnlyList<HotelRoomBuildContext> Hotels { get; init; }

    public required string OutputMode { get; init; }
}

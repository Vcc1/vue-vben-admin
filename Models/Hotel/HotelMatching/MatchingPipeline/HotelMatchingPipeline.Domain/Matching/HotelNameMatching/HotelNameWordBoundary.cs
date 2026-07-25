namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed record HotelNameWordBoundary(
    int CompactOffset,
    bool IsWhitespaceOnly);

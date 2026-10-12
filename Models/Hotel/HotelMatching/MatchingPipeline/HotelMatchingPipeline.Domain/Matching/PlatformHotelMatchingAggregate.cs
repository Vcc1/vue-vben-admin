namespace HotelMatchingPipeline.Domain.Hotels;

public sealed class PlatformHotelMatchingAggregate
{
    private readonly List<Hotel> _matchedHotels = [];
    private readonly List<Hotel> _pendingMatchHotels = [];

    public PlatformHotelMatchingAggregate(Hotel platformHotel)
    {
        ArgumentNullException.ThrowIfNull(platformHotel);

        PlatformHotel = platformHotel;
    }

    public Hotel PlatformHotel { get; private set; }

    public IReadOnlyList<Hotel> MatchedHotels => _matchedHotels;

    public IReadOnlyList<Hotel> PendingMatchHotels => _pendingMatchHotels;

    public void ReplacePlatformHotel(Hotel platformHotel)
    {
        ArgumentNullException.ThrowIfNull(platformHotel);

        PlatformHotel = platformHotel;
    }

    public void AddMatchedHotel(Hotel hotel)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        _matchedHotels.Add(hotel);
    }

    public void AddPendingMatchHotel(Hotel hotel)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        _pendingMatchHotels.Add(hotel);
    }

    public void AddMatchedHotels(IEnumerable<Hotel> hotels)
    {
        ArgumentNullException.ThrowIfNull(hotels);

        _matchedHotels.AddRange(hotels);
    }

    public void AddPendingMatchHotels(IEnumerable<Hotel> hotels)
    {
        ArgumentNullException.ThrowIfNull(hotels);

        _pendingMatchHotels.AddRange(hotels);
    }
}

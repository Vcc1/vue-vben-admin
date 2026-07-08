namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapPriceQueryRequest
{
    public required string PropertyId { get; init; }

    public required DateOnly CheckIn { get; init; }

    public required DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 2;

    public int Rooms { get; init; } = 1;

    public IReadOnlyList<ExpediaTaapRoomOccupancy>? RoomOccupancies { get; init; }

    public string? Sha256Hash { get; init; }

    public string? Locale { get; init; }

    public string? Currency { get; init; }
}

public sealed class ExpediaTaapRoomOccupancy
{
    public int Adults { get; init; }

    public IReadOnlyList<int> ChildrenAges { get; init; } = Array.Empty<int>();
}
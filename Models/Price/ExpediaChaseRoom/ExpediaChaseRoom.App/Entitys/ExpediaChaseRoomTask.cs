namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomTask
{
    public int Id { get; init; }

    public required string PropertyId { get; init; }

    public string? MTHotelCode { get; init; }

    public required DateTime CheckIn { get; init; }

    public required DateTime CheckOut { get; init; }

    public int Rooms { get; init; }

    public int Adults { get; init; }

    public int Priority { get; init; }

    public string Key => $"{PropertyId}|{CheckIn:yyyy-MM-dd}|{CheckOut:yyyy-MM-dd}|{Rooms}|{Adults}";
}
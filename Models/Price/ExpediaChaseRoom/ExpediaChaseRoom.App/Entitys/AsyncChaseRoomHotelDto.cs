namespace ExpediaChaseRoomApp;

public sealed class AsyncChaseRoomHotelDto
{
    public int Adult { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public string MTHotelCode { get; set; } = string.Empty;

    public string SHotelCode { get; set; } = string.Empty;

    public int RoomNum { get; set; }
}
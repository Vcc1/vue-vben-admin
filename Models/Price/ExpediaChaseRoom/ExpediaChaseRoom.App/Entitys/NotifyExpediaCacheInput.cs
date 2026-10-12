namespace ExpediaChaseRoomApp;

public sealed class NotifyExpediaCacheInput
{
    public string PropertyId { get; set; } = string.Empty;

    public string? MTHotelCode { get; set; }

    public int Adults { get; set; }

    public List<NotifyExpediaCacheCheckTime> Check { get; set; } = new();
}

public sealed class NotifyExpediaCacheCheckTime
{
    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomAppOptions
{
    [Required]
    public string GetExpediaChaseRoomHotelUrl { get; set; } = string.Empty;

    public string? NotifyExpediaCachePriceUrl { get; set; }

    public string? PushQueryResultUrl { get; set; }

    public string? PushQueryErrorUrl { get; set; }

    public string? PushCompletePriceCheck { get; set; }

    public string? PushCompletePriceCheckAppKey { get; set; }

    public string? PushCompletePriceCheckAppId { get; set; }

    public int QueueCapacity { get; set; } = 20;

    public int FetchTake { get; set; } = 40;

    public int IdleDelaySeconds { get; set; } = 15;

    public int FetchErrorDelaySeconds { get; set; } = 5;

    public int MetricsLogIntervalSeconds { get; set; } = 30;
}

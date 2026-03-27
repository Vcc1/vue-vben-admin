namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomPriceRecord
{
    public long Id { get; set; }

    public string RoomId { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public string Surplus { get; set; } = string.Empty;

    public string People { get; set; } = string.Empty;

    public string LodgingCategorizedUnit { get; set; } = string.Empty;

    public List<ExpediaChaseRoomChargeOption> LodgingCategorizedUnits { get; set; } = [];

    public string PropertyId { get; set; } = string.Empty;

    public int Rooms { get; set; }

    public int Adults { get; set; }

    public string? Children { get; set; }

    public int? ChildrenNum { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public string OptionId { get; set; } = string.Empty;

    public string SigninEmailAddress { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public int ExpediaChaseId { get; set; }

    public List<ExpediaChaseRoomPriceRefundPolicy> Policies { get; set; } = [];

    public decimal? OriginalTotalPrice { get; set; }
}

public sealed class ExpediaChaseRoomChargeOption
{
    public string Description { get; set; } = string.Empty;

    public string? OptionId { get; set; }

    public string? SubText { get; set; }

    public bool Enabled { get; set; }

    public string Price { get; set; } = string.Empty;

    public bool Selected { get; set; }
}

public sealed class ExpediaChaseRoomPriceRefundPolicy
{
    public long Id { get; set; }

    public string PropertyId { get; set; } = string.Empty;

    public long ExpediaRoomPriceId { get; set; }

    public string PolicyName { get; set; } = string.Empty;

    public string PolicyEqualType { get; set; } = string.Empty;

    public string PolicyEqualTime { get; set; } = string.Empty;

    public string PolicyDisplayName { get; set; } = string.Empty;

    public string PolicyMessages { get; set; } = string.Empty;
}
namespace HotelMatchingPipeline.Domain.Hotels;

[Flags]
public enum HotelDataQualityFlags : long
{
    None = 0,
    CoordinateInvalid = 1,
    CoordinateTypeUnknown = 2,
    RegionNotFound = 4,
    RegionBoundaryAmbiguous = 8,
    PhoneInvalid = 16,
    PhoneCountryConflict = 32,
    PhoneAreaConflict = 64,
    HotelNameMissing = 128,
    RegionCodeMissing = 256,
    MultiplePhoneConflict = 512
}

public enum HotelDataQualityStatus
{
    Unknown = 0,
    Valid = 1,
    Problem = 2
}

public enum HotelCleanStatus
{
    Unknown = 0,
    Ready = 1,
    Failed = 2
}

public enum HotelMatchStatus
{
    Pending = 0,
    Processing = 1,
    NoMatch = 2,
    NeedsReview = 3,
    PendingDelivery = 4,
    Delivered = 5,
    Failed = 6,
    DataProblem = 7
}

public sealed record HotelQualityAssessment(
    HotelDataQualityStatus Status,
    HotelDataQualityFlags Flags,
    IReadOnlyDictionary<string, string?> Details)
{
    public bool IsValid => Status == HotelDataQualityStatus.Valid && Flags == HotelDataQualityFlags.None;

    public static HotelQualityAssessment Valid()
        => new(HotelDataQualityStatus.Valid, HotelDataQualityFlags.None,
            new Dictionary<string, string?>(StringComparer.Ordinal));
}

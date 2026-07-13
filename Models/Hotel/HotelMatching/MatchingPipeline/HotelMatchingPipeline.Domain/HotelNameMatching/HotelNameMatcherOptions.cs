namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed class HotelNameMatcherOptions
{
    private static readonly string[] DefaultIgnoreWords =
    [
        "酒店",
        "大酒店",
        "宾馆",
        "饭店",
        "旅馆",
        "客栈",
        "公寓",
        "度假村",
        "民宿",
        "hotel",
        "hotels",
        "inn",
        "resort",
        "resorts",
        "suite",
        "suites",
        "apartment",
        "apartments",
        "hostel",
        "motel"
    ];

    public IReadOnlyCollection<string> IgnoreWords { get; init; } = DefaultIgnoreWords;
}

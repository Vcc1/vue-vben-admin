namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed class HotelNameMatcherOptions
{
    private static readonly string[] DefaultCjkDictionary =
    [
        "大酒店", "度假村", "商务酒店", "国际酒店", "假日酒店", "精选酒店",
        "酒店", "宾馆", "饭店", "旅馆", "客栈", "公寓", "民宿",
        "希尔顿", "万豪", "喜来登", "洲际", "凯悦", "香格里拉"
    ];

    public IReadOnlyCollection<string> CjkDictionary { get; init; } = DefaultCjkDictionary;
}

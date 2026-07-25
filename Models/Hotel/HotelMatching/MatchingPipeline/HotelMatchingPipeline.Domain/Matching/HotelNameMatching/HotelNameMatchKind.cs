namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public enum HotelNameMatchKind
{
    NotMatched = 0,

    /// <summary>
    /// 双方词一致，且顺序一致。
    /// </summary>
    SameWordsSameOrder = 1,

    /// <summary>
    /// 双方词一致，但顺序不一致。
    /// </summary>
    SameWordsDifferentOrder = 2,

    /// <summary>
    /// 单一一边去除忽略词后，词一致。
    /// </summary>
    OneSideSameWordsAfterIgnoreWordsRemoved = 3
}

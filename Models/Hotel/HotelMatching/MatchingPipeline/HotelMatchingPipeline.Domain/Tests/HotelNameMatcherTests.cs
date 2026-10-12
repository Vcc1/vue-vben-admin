using HotelMatchingPipeline.Domain.HotelNameMatching;
using Xunit;

namespace HotelMatchingPipeline.Domain.Tests;

public sealed class HotelNameMatcherTests
{
    private readonly HotelNameMatcher _matcher = new();

    public static TheoryData<string?, string?, HotelNameMatchKind, HotelNameIgnoredSide> MatchCases => new()
    {
        { "上海希尔顿", "上海希尔顿", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "HILTON SHANGHAI", "hilton shanghai", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "ＨＩＬＴＯＮ　ＳＨＡＮＧＨＡＩ", "hilton shanghai", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "Hôtel Paris", "hotel paris", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "Grand-Hyatt", "Grand Hyatt", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "（Grand Hyatt）", "Grand Hyatt", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "A B", "AB", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "AB", "A B", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "(A B)", "AB", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "A B C", "AB C", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "A B C", "A BC", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "A B", "B A", HotelNameMatchKind.SameWordsDifferentOrder, HotelNameIgnoredSide.None },
        { "Grand Hyatt Shanghai", "Shanghai Grand Hyatt", HotelNameMatchKind.SameWordsDifferentOrder, HotelNameIgnoredSide.None },
        { "A A B", "B A A", HotelNameMatchKind.SameWordsDifferentOrder, HotelNameIgnoredSide.None },
        { "Grand-Hyatt Shanghai", "Shanghai Grand Hyatt", HotelNameMatchKind.SameWordsDifferentOrder, HotelNameIgnoredSide.None },
        { "上海希尔顿酒店", "上海希尔顿", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "上海希尔顿大酒店", "上海希尔顿", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "上海希尔顿", "上海希尔顿宾馆", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Grand Hotel", "Grand", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Grand", "Grand Resort", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A B Hotel", "B A", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A Hotel B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "AB", "A Hotel B", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Al Hayat", "Al Hayat Hotel Apartments", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { null, null, HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "", "   ", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Shanghai", "Beijing", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A B", "A C", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A-B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A/B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A·B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A_B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A.B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A B", "BA", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Grand Hotel", "Grand Resort", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Hotelton", "ton", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A-B C", "AB C", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A/B C", "A BC", HotelNameMatchKind.SameWordsSameOrder, HotelNameIgnoredSide.None },
        { "A B/C", "A BC", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A/B C", "AB C", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "A-Hotel-B", "AB", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "AB", "A/Hotel/B", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "AB", "A-Hotel-B", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "酒店", "", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None },
        { "Hotel", "", HotelNameMatchKind.NotMatched, HotelNameIgnoredSide.None }
    };

    [Theory]
    [MemberData(nameof(MatchCases))]
    public void Match_returns_expected_result(
        string? left,
        string? right,
        HotelNameMatchKind expectedKind,
        HotelNameIgnoredSide expectedIgnoredSide)
    {
        var result = _matcher.Match(left, right);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedKind != HotelNameMatchKind.NotMatched, result.IsMatch);
        Assert.Equal(expectedIgnoredSide, result.IgnoredSide);
    }

    [Fact]
    public void Match_does_not_remove_ignore_words()
    {
        var result = _matcher.Match("Al Hayat", "Al Hayat Hotel Apartments");

        Assert.Equal(HotelNameMatchKind.NotMatched, result.Kind);
        Assert.Equal(HotelNameIgnoredSide.None, result.IgnoredSide);
        Assert.Empty(result.RemovedIgnoreWords);
    }
}

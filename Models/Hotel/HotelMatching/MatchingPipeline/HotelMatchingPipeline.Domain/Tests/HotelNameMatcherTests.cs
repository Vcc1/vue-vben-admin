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
        { "上海希尔顿酒店", "上海希尔顿", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Left },
        { "上海希尔顿大酒店", "上海希尔顿", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Left },
        { "上海希尔顿", "上海希尔顿宾馆", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Right },
        { "Grand Hotel", "Grand", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Left },
        { "Grand", "Grand Resort", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Right },
        { "A B Hotel", "B A", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Left },
        { "A Hotel B", "AB", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Left },
        { "AB", "A Hotel B", HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, HotelNameIgnoredSide.Right },
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

    [Theory]
    [InlineData("上海希尔顿大酒店", "上海希尔顿", HotelNameIgnoredSide.Left, "大酒店")]
    [InlineData("Grand", "Grand Resort", HotelNameIgnoredSide.Right, "resort")]
    public void Match_reports_removed_ignore_words(
        string left,
        string right,
        HotelNameIgnoredSide expectedSide,
        string expectedRemovedWord)
    {
        var result = _matcher.Match(left, right);

        Assert.Equal(HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, result.Kind);
        Assert.Equal(expectedSide, result.IgnoredSide);
        Assert.Contains(expectedRemovedWord, result.RemovedIgnoreWords);
    }

    [Fact]
    public void Match_supports_custom_ignore_words()
    {
        var options = new HotelNameMatcherOptions
        {
            IgnoreWords = ["palace"]
        };

        var result = _matcher.Match("Aurora Palace", "Aurora", options);

        Assert.Equal(HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved, result.Kind);
        Assert.Equal(HotelNameIgnoredSide.Left, result.IgnoredSide);
        Assert.Equal(["palace"], result.RemovedIgnoreWords);
    }

    [Fact]
    public void Match_keeps_original_cleaned_names_in_result_after_ignore_word_match()
    {
        var result = _matcher.Match("上海希尔顿酒店", "上海希尔顿");

        Assert.Equal("上海希尔顿酒店", result.LeftCleanedName);
        Assert.Equal("上海希尔顿", result.RightCleanedName);
    }
}

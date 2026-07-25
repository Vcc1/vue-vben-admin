namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed record HotelNameMatchResult
{
    private HotelNameMatchResult(
        HotelNameMatchKind kind,
        string leftCleanedName,
        string rightCleanedName,
        IReadOnlyList<string> leftWords,
        IReadOnlyList<string> rightWords,
        HotelNameIgnoredSide ignoredSide,
        IReadOnlyList<string> removedIgnoreWords)
    {
        Kind = kind;
        LeftCleanedName = leftCleanedName;
        RightCleanedName = rightCleanedName;
        LeftWords = leftWords;
        RightWords = rightWords;
        IgnoredSide = ignoredSide;
        RemovedIgnoreWords = removedIgnoreWords;
    }

    public HotelNameMatchKind Kind { get; }

    public bool IsMatch => Kind != HotelNameMatchKind.NotMatched;

    public string LeftCleanedName { get; }

    public string RightCleanedName { get; }

    public IReadOnlyList<string> LeftWords { get; }

    public IReadOnlyList<string> RightWords { get; }

    public HotelNameIgnoredSide IgnoredSide { get; }

    public IReadOnlyList<string> RemovedIgnoreWords { get; }

    public static HotelNameMatchResult NotMatched(HotelNameWordView left, HotelNameWordView right)
        => new(
            HotelNameMatchKind.NotMatched,
            left.CleanedName,
            right.CleanedName,
            left.Words,
            right.Words,
            HotelNameIgnoredSide.None,
            []);

    public static HotelNameMatchResult Matched(
        HotelNameMatchKind kind,
        HotelNameWordView left,
        HotelNameWordView right,
        HotelNameIgnoredSide ignoredSide = HotelNameIgnoredSide.None,
        IReadOnlyList<string>? removedIgnoreWords = null)
        => new(
            kind,
            left.CleanedName,
            right.CleanedName,
            left.Words,
            right.Words,
            ignoredSide,
            removedIgnoreWords ?? []);
}

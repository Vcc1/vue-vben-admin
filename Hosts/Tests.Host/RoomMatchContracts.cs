using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using OTADomain.Matchings;

namespace Tests.Host.DebugTools.Contracts;

internal sealed class RoomMatchEvaluateRequest
{
    public string? LeftRoomName { get; set; }

    public string? RightRoomName { get; set; }

    public string? WordDictSource { get; set; } = "official";

    public string? CleanFormatType { get; set; } = "strongNormalize";
}

internal sealed class RoomMatchEvaluateResponse
{
    public bool CanMatch { get; set; }

    public string Summary { get; set; } = string.Empty;

    public int OnlineMatchType { get; set; }

    public string OnlineMatchTypeName { get; set; } = string.Empty;

    public int BoundaryDistance { get; set; }

    public string MatchDirection { get; set; } = "ota->supplier";

    public DateTimeOffset ComparedAt { get; set; }

    public RoomMatchContextView Left { get; set; } = new();

    public RoomMatchContextView Right { get; set; } = new();

    public RoomMatcherResultView? BestMatch { get; set; }

    public IReadOnlyList<RoomMatcherResultView> MatcherResults { get; set; } = [];
}

internal sealed class RoomMatchContextView
{
    public string RoomNameOriginal { get; set; } = string.Empty;

    public string RoomNameCleaned { get; set; } = string.Empty;

    public string? ErrorMsg { get; set; }

    public bool IsUnknownChar { get; set; }

    public IReadOnlyList<RoomWordView> Words { get; set; } = [];
}

internal sealed class RoomWordView
{
    public int Index { get; set; }

    public string WordText { get; set; } = string.Empty;

    public int WordId { get; set; }

    public long WordTypeId { get; set; }

    public string? WordTypeName { get; set; }

    public bool IsMustNeed { get; set; }

    public int? EffectType { get; set; }

    public string Category { get; set; } = "other";

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<decimal> Numbers { get; set; } = [];
}

internal sealed class RoomMatcherResultView
{
    public string MatcherName { get; set; } = string.Empty;

    public bool IsMatch { get; set; }

    public int MatchType { get; set; }

    public decimal? MatchScore { get; set; }
}

internal static class RoomMatchMapper
{
    public static RoomAnalysisSetting BuildSetting(RoomMatchEvaluateRequest request)
    {
        return new RoomAnalysisSetting
        {
            WordDictSource = string.Equals(request.WordDictSource, "contrastOfficial", StringComparison.OrdinalIgnoreCase)
                ? WordDictSource.Contrast_Official
                : WordDictSource.Official,
            CleanFormatType = string.Equals(request.CleanFormatType, "basicNormalize", StringComparison.OrdinalIgnoreCase)
                ? CleanFormatType.BASIC_NORMALIZE
                : CleanFormatType.STRONG_NORMALIZE
        };
    }

    public static RoomMatchContextView ToView(RoomContext context)
    {
        return new RoomMatchContextView
        {
            RoomNameOriginal = context.RoomNameOriginal ?? string.Empty,
            RoomNameCleaned = context.RoomNameCleaned ?? string.Empty,
            ErrorMsg = context.ErrorMsg,
            IsUnknownChar = context.IsUnknownChar,
            Words = context.RoomNameWords
                .Select((word, index) => new RoomWordView
                {
                    Index = index,
                    WordText = word.WordText ?? string.Empty,
                    WordId = word.WordAttr?.Id ?? 0,
                    WordTypeId = word.WordAttr?.WordTypeId ?? 0,
                    WordTypeName = word.WordAttr?.WordTypeName,
                    IsMustNeed = word.WordAttr?.IsMustNeed ?? false,
                    EffectType = word.WordAttr is null ? null : (int)word.WordAttr.EffectType,
                    Category = ResolveCategory(word),
                    Tags = ResolveTags(word),
                    Numbers = word.Numbers?.ToArray() ?? []
                })
                .ToArray()
        };
    }

    private static string ResolveCategory(RoomWord word)
    {
        var attr = word.WordAttr;
        if (attr is null)
        {
            return "other";
        }

        if (attr.WordTypeId == 22)
        {
            return "symbol";
        }

        if (attr.EffectType == EffectType.Negative)
        {
            return "negative";
        }

        if (attr.EffectType == EffectType.Positive)
        {
            return "positive";
        }

        return attr.IsMustNeed ? "must" : "other";
    }

    private static IReadOnlyList<string> ResolveTags(RoomWord word)
    {
        var attr = word.WordAttr;
        if (attr is null)
        {
            return [];
        }

        var tags = new List<string>(2);

        if (attr.WordTypeId == 22)
        {
            tags.Add("symbol");
            return tags;
        }

        if (attr.IsMustNeed)
        {
            tags.Add("must");
        }

        if (attr.EffectType == EffectType.Positive)
        {
            tags.Add("positive");
        }
        else if (attr.EffectType == EffectType.Negative)
        {
            tags.Add("negative");
        }

        if (tags.Count == 0)
        {
            tags.Add("other");
        }

        return tags;
    }
}

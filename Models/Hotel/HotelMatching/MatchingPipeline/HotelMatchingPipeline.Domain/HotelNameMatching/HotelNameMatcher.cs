using System.Globalization;
using System.Text;

namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed class HotelNameMatcher
{
    public HotelNameMatchResult Match(
        string? leftHotelName,
        string? rightHotelName,
        HotelNameMatcherOptions? options = null)
    {
        options ??= new HotelNameMatcherOptions();

        var left = BuildWordView(leftHotelName);
        var right = BuildWordView(rightHotelName);

        if (TryMatchWords(left, right, out var kind))
        {
            return HotelNameMatchResult.Matched(kind, left, right);
        }

        var normalizedIgnoreWords = NormalizeIgnoreWords(options.IgnoreWords);

        var leftIgnore = RemoveIgnoreWords(left.NormalizedSource, normalizedIgnoreWords);
        if (leftIgnore.RemovedWords.Count > 0)
        {
            var leftAfterIgnore = BuildWordView(leftIgnore.NameWithoutIgnoreWords);
            if (TryMatchWords(leftAfterIgnore, right, out _))
            {
                return HotelNameMatchResult.Matched(
                    HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved,
                    left,
                    right,
                    HotelNameIgnoredSide.Left,
                    leftIgnore.RemovedWords);
            }
        }

        var rightIgnore = RemoveIgnoreWords(right.NormalizedSource, normalizedIgnoreWords);
        if (rightIgnore.RemovedWords.Count > 0)
        {
            var rightAfterIgnore = BuildWordView(rightIgnore.NameWithoutIgnoreWords);
            if (TryMatchWords(left, rightAfterIgnore, out _))
            {
                return HotelNameMatchResult.Matched(
                    HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved,
                    left,
                    right,
                    HotelNameIgnoredSide.Right,
                    rightIgnore.RemovedWords);
            }
        }

        return HotelNameMatchResult.NotMatched(left, right);
    }

    public static HotelNameWordView BuildWordView(string? hotelName)
    {
        if (string.IsNullOrWhiteSpace(hotelName))
        {
            return new HotelNameWordView(string.Empty, [], string.Empty, [], string.Empty);
        }

        var normalized = hotelName.Normalize(NormalizationForm.FormD);
        var words = new List<string>();
        var boundaries = new List<HotelNameWordBoundary>();
        var compactName = new StringBuilder(normalized.Length);
        var normalizedSource = new StringBuilder(normalized.Length);
        var currentWord = new StringBuilder();
        var separatorHasWhitespace = false;
        var separatorHasSymbol = false;

        foreach (var originalChar in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(originalChar) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = char.ToLowerInvariant(ToHalfWidth(originalChar));
            normalizedSource.Append(c);
            if (IsWordChar(c))
            {
                if (currentWord.Length == 0 && words.Count > 0)
                {
                    boundaries.Add(new HotelNameWordBoundary(
                        compactName.Length,
                        separatorHasWhitespace && !separatorHasSymbol));
                    separatorHasWhitespace = false;
                    separatorHasSymbol = false;
                }

                currentWord.Append(c);
                compactName.Append(c);
                continue;
            }

            FlushCurrentWord(currentWord, words);
            if (words.Count > 0)
            {
                separatorHasWhitespace |= char.IsWhiteSpace(c);
                separatorHasSymbol |= !char.IsWhiteSpace(c);
            }
        }

        FlushCurrentWord(currentWord, words);

        var cleanedName = string.Join(' ', words);

        return new HotelNameWordView(
            cleanedName,
            words,
            compactName.ToString().Normalize(NormalizationForm.FormC),
            boundaries,
            normalizedSource.ToString().Normalize(NormalizationForm.FormC));
    }

    public static string Normalize(string? hotelName)
        => BuildWordView(hotelName).CleanedName;

    private static bool TryMatchWords(
        HotelNameWordView left,
        HotelNameWordView right,
        out HotelNameMatchKind kind)
    {
        kind = HotelNameMatchKind.NotMatched;

        if (left.Words.Count == 0 || right.Words.Count == 0)
        {
            return false;
        }

        if (HaveSameWordsSameOrder(left.Words, right.Words) ||
            HaveSameWordsWithOnlySpaceCompaction(left, right))
        {
            kind = HotelNameMatchKind.SameWordsSameOrder;
            return true;
        }

        if (HaveSameWordBag(left.Words, right.Words))
        {
            kind = HotelNameMatchKind.SameWordsDifferentOrder;
            return true;
        }

        return false;
    }

    private static bool HaveSameWordsSameOrder(IReadOnlyList<string> leftWords, IReadOnlyList<string> rightWords)
    {
        if (leftWords.Count != rightWords.Count)
        {
            return false;
        }

        for (var i = 0; i < leftWords.Count; i++)
        {
            if (!string.Equals(leftWords[i], rightWords[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HaveSameWordsWithOnlySpaceCompaction(HotelNameWordView left, HotelNameWordView right)
    {
        if (left.Words.Count == right.Words.Count)
        {
            return false;
        }

        if (left.CompactName.Length == 0 ||
            !string.Equals(left.CompactName, right.CompactName, StringComparison.Ordinal))
        {
            return false;
        }

        var leftBoundaries = left.Boundaries.ToDictionary(boundary => boundary.CompactOffset);
        var rightBoundaries = right.Boundaries.ToDictionary(boundary => boundary.CompactOffset);

        return HasOnlyWhitespaceAtRemovedBoundaries(leftBoundaries, rightBoundaries) &&
               HasOnlyWhitespaceAtRemovedBoundaries(rightBoundaries, leftBoundaries);
    }

    private static bool HasOnlyWhitespaceAtRemovedBoundaries(
        IReadOnlyDictionary<int, HotelNameWordBoundary> sourceBoundaries,
        IReadOnlyDictionary<int, HotelNameWordBoundary> targetBoundaries)
        => sourceBoundaries.Values.All(boundary =>
            targetBoundaries.ContainsKey(boundary.CompactOffset) || boundary.IsWhitespaceOnly);

    private static bool HaveSameWordBag(IReadOnlyList<string> leftWords, IReadOnlyList<string> rightWords)
    {
        if (leftWords.Count != rightWords.Count)
        {
            return false;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var word in leftWords)
        {
            counts.TryGetValue(word, out var count);
            counts[word] = count + 1;
        }

        foreach (var word in rightWords)
        {
            if (!counts.TryGetValue(word, out var count))
            {
                return false;
            }

            if (count == 1)
            {
                counts.Remove(word);
            }
            else
            {
                counts[word] = count - 1;
            }
        }

        return counts.Count == 0;
    }

    private static IReadOnlyList<string> NormalizeIgnoreWords(IReadOnlyCollection<string> ignoreWords)
        => ignoreWords
            .Select(Normalize)
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(word => word.Length)
            .ToArray();

    private static (string NameWithoutIgnoreWords, IReadOnlyList<string> RemovedWords) RemoveIgnoreWords(
        string cleanedName,
        IReadOnlyList<string> ignoreWords)
    {
        var name = cleanedName;
        var removedWords = new List<string>();

        foreach (var ignoreWord in ignoreWords)
        {
            name = RemoveIgnoreWord(name, ignoreWord, out var removed);
            if (!removed)
            {
                continue;
            }

            removedWords.Add(ignoreWord);
        }

        return (name, removedWords);
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || IsCjk(c);

    private static bool IsCjk(char c)
        => c is >= '\u3400' and <= '\u9FFF';

    private static char ToHalfWidth(char c)
    {
        if (c == '\u3000')
        {
            return ' ';
        }

        return c is >= '\uFF01' and <= '\uFF5E'
            ? (char)(c - 0xFEE0)
            : c;
    }

    private static void FlushCurrentWord(StringBuilder currentWord, ICollection<string> words)
    {
        if (currentWord.Length == 0)
        {
            return;
        }

        words.Add(currentWord.ToString().Normalize(NormalizationForm.FormC));
        currentWord.Clear();
    }

    private static string RemoveIgnoreWord(string name, string ignoreWord, out bool removed)
    {
        removed = false;
        var searchStart = 0;
        var requireAsciiWordBoundary = ignoreWord.All(c => IsAsciiLetterOrDigit(c) || c == ' ');

        while (searchStart <= name.Length - ignoreWord.Length)
        {
            var index = name.IndexOf(ignoreWord, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            var endIndex = index + ignoreWord.Length;
            var startsInsideAsciiWord = index > 0 && IsAsciiLetterOrDigit(name[index - 1]);
            var endsInsideAsciiWord = endIndex < name.Length && IsAsciiLetterOrDigit(name[endIndex]);

            if (requireAsciiWordBoundary && (startsInsideAsciiWord || endsInsideAsciiWord))
            {
                searchStart = endIndex;
                continue;
            }

            name = name.Remove(index, ignoreWord.Length).Insert(index, " ");
            removed = true;
            searchStart = index + 1;
        }

        return name;
    }

    private static bool IsAsciiLetterOrDigit(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}

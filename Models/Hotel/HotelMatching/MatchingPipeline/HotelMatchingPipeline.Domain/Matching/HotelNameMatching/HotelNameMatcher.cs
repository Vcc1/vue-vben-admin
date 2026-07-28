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

        var left = BuildWordView(leftHotelName, options.CjkDictionary);
        var right = BuildWordView(rightHotelName, options.CjkDictionary);

        if (TryMatchWords(left, right, out var kind))
        {
            return HotelNameMatchResult.Matched(kind, left, right);
        }

        return HotelNameMatchResult.NotMatched(left, right);
    }

    public static HotelNameWordView BuildWordView(string? hotelName)
        => BuildWordView(hotelName, new HotelNameMatcherOptions().CjkDictionary);

    public static HotelNameWordView BuildWordView(
        string? hotelName,
        IReadOnlyCollection<string>? cjkDictionary)
    {
        if (string.IsNullOrWhiteSpace(hotelName))
        {
            return new HotelNameWordView(string.Empty, [], string.Empty, []);
        }

        var normalized = hotelName.Normalize(NormalizationForm.FormD);
        var words = new List<string>();
        var boundaries = new List<HotelNameWordBoundary>();
        var compactName = new StringBuilder(normalized.Length);
        var currentWord = new StringBuilder();
        var separatorHasWhitespace = false;
        var separatorHasSymbol = false;

        var dictionary = (cjkDictionary ?? []).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Normalize(NormalizationForm.FormC))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(x => x.Length)
            .ToArray();

        for (var index = 0; index < normalized.Length; index++)
        {
            var originalChar = normalized[index];
            if (CharUnicodeInfo.GetUnicodeCategory(originalChar) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = char.ToLowerInvariant(ToHalfWidth(originalChar));

            if (IsCjk(c))
            {
                FlushCurrentWord(currentWord, words);
                var start = index;
                while (index + 1 < normalized.Length && IsCjk(ToHalfWidth(normalized[index + 1])))
                {
                    index++;
                }

                var run = normalized[start..(index + 1)].ToLowerInvariant();
                foreach (var token in TokenizeCjkRun(run, dictionary))
                {
                    if (words.Count > 0)
                    {
                        boundaries.Add(new HotelNameWordBoundary(compactName.Length, false));
                    }

                    words.Add(token);
                    compactName.Append(token);
                }

                separatorHasWhitespace = false;
                separatorHasSymbol = false;
                continue;
            }

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

        var cleanedName = words.Count > 0 && words.All(word => word.All(IsCjk))
            ? string.Concat(words)
            : string.Join(' ', words);

        return new HotelNameWordView(
            cleanedName,
            words,
            compactName.ToString().Normalize(NormalizationForm.FormC),
            boundaries);
    }

    static IReadOnlyList<string> TokenizeCjkRun(string run, IReadOnlyList<string> dictionary)
    {
        var result = new List<string>();
        for (var index = 0; index < run.Length;)
        {
            var token = dictionary.FirstOrDefault(candidate =>
                index + candidate.Length <= run.Length
                && run.AsSpan(index, candidate.Length).Equals(candidate.AsSpan(), StringComparison.Ordinal));

            token ??= run[index].ToString();
            result.Add(token);
            index += token.Length;
        }

        return result;
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

}

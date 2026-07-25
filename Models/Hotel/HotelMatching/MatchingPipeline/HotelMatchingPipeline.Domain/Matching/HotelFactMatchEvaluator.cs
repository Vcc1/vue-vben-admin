using System.Globalization;
using System.Text;
using HotelMatchingPipeline.Domain.HotelNameMatching;

namespace HotelMatchingPipeline.Domain.Hotels;

public sealed class HotelFactMatchEvaluator
{
    readonly HotelNameMatcher _nameMatcher;

    public HotelFactMatchEvaluator(HotelNameMatcher? nameMatcher = null)
    {
        _nameMatcher = nameMatcher ?? new HotelNameMatcher();
    }

    public HotelMatchEvaluation Evaluate(HotelMatchingWorkItem workItem, int maximumDistanceMeters)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        var matches = workItem.NearbyOtaHotels
            .Where(fact => !string.IsNullOrWhiteSpace(fact.MTHotelCode))
            .GroupBy(fact => fact.MTHotelCode!, StringComparer.OrdinalIgnoreCase)
            .Select(group => EvaluateCandidate(workItem, group.Key, group, maximumDistanceMeters))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();

        return new HotelMatchEvaluation(
            workItem.SupHotelId,
            workItem.PendingSupplier.SupplierId
                ?? throw new InvalidOperationException("Pending supplier hotel is missing SupId."),
            workItem.PendingSupplier.HotelCode,
            matches);
    }

    HotelMatchedCandidate? EvaluateCandidate(
        HotelMatchingWorkItem workItem,
        string mtHotelCode,
        IEnumerable<HotelMatchFact> otaFacts,
        int maximumDistanceMeters)
    {
        var facts = otaFacts
            .Concat(workItem.NearbySupplierHotels.Where(fact =>
                string.Equals(fact.MTHotelCode, mtHotelCode, StringComparison.OrdinalIgnoreCase)))
            .Concat(workItem.OtaLinkedSupplierHotels.Where(fact =>
                string.Equals(fact.MTHotelCode, mtHotelCode, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(fact => (fact.Source, fact.HotelId))
            .ToArray();

        var name = FindNameEvidence(workItem.PendingSupplier, facts);
        var coordinate = FindCoordinateEvidence(workItem.PendingSupplier, facts, maximumDistanceMeters);
        if (name is null || coordinate is null)
        {
            return null;
        }

        return new HotelMatchedCandidate(
            mtHotelCode,
            name.Value.Evidence,
            name.Value.MatchKind,
            coordinate,
            FindStarEvidence(workItem.PendingSupplier, facts),
            FindAddressEvidence(workItem.PendingSupplier, facts),
            FindPhoneEvidence(workItem.PendingSupplier, facts),
            facts.Count(fact => fact.Source == HotelMatchFactSource.Ota),
            facts.Count(fact => fact.Source == HotelMatchFactSource.Supplier));
    }

    (HotelMatchEvidence Evidence, HotelNameMatchKind MatchKind)? FindNameEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        var candidates = new List<(HotelMatchEvidence Evidence, HotelNameMatchKind MatchKind, int Rank)>();
        foreach (var pendingName in Names(pending))
        {
            foreach (var fact in facts)
            {
                foreach (var factName in Names(fact))
                {
                    var result = _nameMatcher.Match(pendingName, factName);
                    if (result.IsMatch)
                    {
                        candidates.Add((
                            new HotelMatchEvidence(fact.Source, fact.HotelId, pendingName, factName),
                            result.Kind,
                            NameRank(result.Kind)));
                    }
                }
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Evidence.Source)
            .ThenBy(candidate => candidate.Evidence.HotelId)
            .Select(candidate => ((HotelMatchEvidence Evidence, HotelNameMatchKind MatchKind)?)(
                candidate.Evidence,
                candidate.MatchKind))
            .FirstOrDefault();
    }

    static HotelMatchEvidence? FindCoordinateEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts,
        int maximumDistanceMeters)
    {
        if (!pending.Latitude.HasValue || !pending.Longitude.HasValue)
        {
            return null;
        }

        return facts
            .Where(fact => fact.Latitude.HasValue && fact.Longitude.HasValue)
            .Select(fact => new
            {
                Fact = fact,
                Distance = DistanceMeters(
                    pending.Latitude.Value,
                    pending.Longitude.Value,
                    fact.Latitude!.Value,
                    fact.Longitude!.Value)
            })
            .Where(candidate => candidate.Distance <= maximumDistanceMeters)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Fact.Source)
            .ThenBy(candidate => candidate.Fact.HotelId)
            .Select(candidate => new HotelMatchEvidence(
                candidate.Fact.Source,
                candidate.Fact.HotelId,
                CoordinateValue(pending),
                CoordinateValue(candidate.Fact),
                candidate.Distance))
            .FirstOrDefault();
    }

    static HotelMatchEvidence? FindStarEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        if (!pending.StarLevel.HasValue)
        {
            return null;
        }

        return facts
            .Where(fact => fact.StarLevel == pending.StarLevel)
            .OrderBy(fact => fact.Source)
            .ThenBy(fact => fact.HotelId)
            .Select(fact => new HotelMatchEvidence(
                fact.Source,
                fact.HotelId,
                pending.StarLevel.Value.ToString(CultureInfo.InvariantCulture),
                fact.StarLevel!.Value.ToString(CultureInfo.InvariantCulture)))
            .FirstOrDefault();
    }

    static HotelMatchEvidence? FindAddressEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        var normalizedPending = NormalizeComparableText(pending.Address);
        if (normalizedPending.Length == 0)
        {
            return null;
        }

        return facts
            .Where(fact => string.Equals(
                normalizedPending,
                NormalizeComparableText(fact.Address),
                StringComparison.Ordinal))
            .OrderBy(fact => fact.Source)
            .ThenBy(fact => fact.HotelId)
            .Select(fact => new HotelMatchEvidence(
                fact.Source,
                fact.HotelId,
                pending.Address,
                fact.Address))
            .FirstOrDefault();
    }

    static HotelMatchEvidence? FindPhoneEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        var pendingPhones = pending.Phones
            .Where(phone => !string.IsNullOrWhiteSpace(phone))
            .ToHashSet(StringComparer.Ordinal);
        if (pendingPhones.Count == 0)
        {
            return null;
        }

        foreach (var fact in facts.OrderBy(fact => fact.Source).ThenBy(fact => fact.HotelId))
        {
            var phone = fact.Phones.FirstOrDefault(pendingPhones.Contains);
            if (phone is not null)
            {
                return new HotelMatchEvidence(fact.Source, fact.HotelId, phone, phone);
            }
        }

        return null;
    }

    static IEnumerable<string> Names(HotelMatchFact fact)
    {
        if (!string.IsNullOrWhiteSpace(fact.HotelName))
        {
            yield return fact.HotelName;
        }
        if (!string.IsNullOrWhiteSpace(fact.HotelNameEn)
            && !string.Equals(fact.HotelName, fact.HotelNameEn, StringComparison.OrdinalIgnoreCase))
        {
            yield return fact.HotelNameEn;
        }
    }

    static int NameRank(HotelNameMatchKind kind)
        => kind switch
        {
            HotelNameMatchKind.SameWordsSameOrder => 3,
            HotelNameMatchKind.SameWordsDifferentOrder => 2,
            HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved => 1,
            _ => 0
        };

    static string CoordinateValue(HotelMatchFact fact)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{fact.Latitude:0.#######},{fact.Longitude:0.#######}");

    static string NormalizeComparableText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToLowerInvariant(character));
            }
        }
        return result.ToString();
    }

    static double DistanceMeters(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        const double radius = 6371008.8;
        var lat1 = (double)latitude1 * Math.PI / 180;
        var lat2 = (double)latitude2 * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = ((double)longitude2 - (double)longitude1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

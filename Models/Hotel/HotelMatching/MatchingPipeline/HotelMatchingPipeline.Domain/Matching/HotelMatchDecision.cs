using HotelMatchingPipeline.Domain.HotelNameMatching;

namespace HotelMatchingPipeline.Domain.Hotels;

public sealed record HotelMatchCandidate(
    string MTHotelCode,
    double DistanceMeters,
    HotelNameMatchResult NameResult,
    bool RegionMatches,
    bool PhoneMatches,
    bool PhoneConflict,
    bool IsQualityValid,
    IReadOnlyList<string> RejectionReasons,
    string? MatchedAlias,
    int Score)
{
    public bool IsEligible => RejectionReasons.Count == 0;
}

public enum HotelMatchDecisionKind
{
    NoMatch = 0,
    AutoMatched = 1,
    NeedsReview = 2,
    DataProblem = 3
}

public sealed record HotelMatchDecision(
    HotelMatchDecisionKind Kind,
    string? MTHotelCode,
    IReadOnlyList<HotelMatchCandidate> Candidates,
    IReadOnlyList<string> Reasons)
{
    public static HotelMatchDecision DataProblem(string reason)
        => new(HotelMatchDecisionKind.DataProblem, null, [], [reason]);
}

public sealed class HotelMatchEvaluator
{
    readonly HotelNameMatcher _nameMatcher;

    public HotelMatchEvaluator(HotelNameMatcher? nameMatcher = null)
    {
        _nameMatcher = nameMatcher ?? new HotelNameMatcher();
    }

    public HotelMatchDecision Evaluate(
        CleanedHotel supplier,
        IReadOnlyCollection<CleanedHotel> otaCandidates,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> aliasesByMtCode)
    {
        if (!supplier.IsQualityValid)
        {
            return HotelMatchDecision.DataProblem("supplier_quality_problem");
        }

        var candidates = new List<HotelMatchCandidate>();
        foreach (var ota in otaCandidates)
        {
            var reasons = new List<string>();
            if (!ota.IsQualityValid)
            {
                reasons.Add("candidate_quality_problem");
            }

            var distance = DistanceMeters(supplier.Latitude, supplier.Longitude, ota.Latitude, ota.Longitude);
            if (double.IsNaN(distance) || distance > 3000)
            {
                reasons.Add("outside_recall_radius");
            }

            if (!RegionMatches(supplier, ota))
            {
                reasons.Add("region_conflict");
            }

            var nameResult = _nameMatcher.Match(supplier.HotelName, ota.HotelName);
            if (!nameResult.IsMatch)
            {
                foreach (var supplierName in new[] { supplier.HotelName, supplier.HotelNameEn })
                {
                    if (string.IsNullOrWhiteSpace(supplierName))
                    {
                        continue;
                    }

                    foreach (var otaName in new[] { ota.HotelName, ota.HotelNameEn })
                    {
                        if (string.IsNullOrWhiteSpace(otaName))
                        {
                            continue;
                        }

                        var alternateResult = _nameMatcher.Match(supplierName, otaName);
                        if (alternateResult.IsMatch)
                        {
                            nameResult = alternateResult;
                            break;
                        }
                    }

                    if (nameResult.IsMatch)
                    {
                        break;
                    }
                }
            }
            string? matchedAlias = null;
            if (!nameResult.IsMatch && aliasesByMtCode.TryGetValue(ota.HotelCode, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    var aliasResult = _nameMatcher.Match(supplier.HotelName, alias);
                    if (aliasResult.IsMatch)
                    {
                        matchedAlias = alias;
                        nameResult = aliasResult;
                        break;
                    }
                }
            }

            if (!nameResult.IsMatch)
            {
                reasons.Add("name_mismatch");
            }

            var phoneComparison = ComparePhones(supplier, ota);
            if (phoneComparison.Conflict)
            {
                reasons.Add("phone_conflict");
            }

            if (distance > 300 && distance <= 1000 && (!phoneComparison.Matches || !RegionMatches(supplier, ota)))
            {
                reasons.Add("phone_or_region_required_between_300m_and_1km");
            }

            var score = Score(distance, nameResult, phoneComparison.Matches, !phoneComparison.Conflict, matchedAlias is not null);
            candidates.Add(new HotelMatchCandidate(
                ota.HotelCode,
                distance,
                nameResult,
                RegionMatches(supplier, ota),
                phoneComparison.Matches,
                phoneComparison.Conflict,
                ota.IsQualityValid,
                reasons,
                matchedAlias,
                score));
        }

        var eligible = candidates.Where(x => x.RejectionReasons.Count == 0).OrderByDescending(x => x.Score).ThenBy(x => x.DistanceMeters).ToArray();
        if (eligible.Length == 0)
        {
            return new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, candidates, ["no_candidate_passed_rules"]);
        }

        if (eligible.Length > 1)
        {
            return new HotelMatchDecision(HotelMatchDecisionKind.NeedsReview, null, candidates, ["ambiguous_candidates"]);
        }

        if (eligible[0].DistanceMeters > 1000)
        {
            return new HotelMatchDecision(HotelMatchDecisionKind.NeedsReview, null, candidates, ["outside_auto_radius"]);
        }

        return new HotelMatchDecision(HotelMatchDecisionKind.AutoMatched, eligible[0].MTHotelCode, candidates, ["high_confidence_candidate"]);
    }

    static bool RegionMatches(CleanedHotel left, CleanedHotel right)
        => !string.IsNullOrWhiteSpace(left.RegionLevel1Code)
            && CountryCodeNormalizer.AreEquivalent(left.RegionLevel1Code, right.RegionLevel1Code)
            && (string.IsNullOrWhiteSpace(left.RegionLevel3Code)
                || string.IsNullOrWhiteSpace(right.RegionLevel3Code)
                || string.Equals(left.RegionLevel3Code, right.RegionLevel3Code, StringComparison.OrdinalIgnoreCase));

    static (bool Matches, bool Conflict) ComparePhones(CleanedHotel left, CleanedHotel right)
    {
        var leftPhones = left.NormalizedPhones.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        var rightPhones = right.NormalizedPhones.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (leftPhones.Count == 0 || rightPhones.Count == 0)
        {
            return (false, false);
        }

        return (leftPhones.Overlaps(rightPhones), !leftPhones.Overlaps(rightPhones));
    }

    static int Score(double distance, HotelNameMatchResult name, bool phoneMatches, bool phoneOk, bool alias)
    {
        var score = distance <= 300 ? 50 : 20;
        score += name.Kind == HotelNameMatchKind.SameWordsSameOrder ? 40 : 30;
        score += phoneMatches ? 25 : 0;
        score += phoneOk ? 5 : -50;
        score += alias ? -5 : 0;
        return score;
    }

    static double DistanceMeters(decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        if (!latitude1.HasValue || !longitude1.HasValue || !latitude2.HasValue || !longitude2.HasValue)
        {
            return double.NaN;
        }

        const double radius = 6371008.8;
        var lat1 = (double)latitude1.Value * Math.PI / 180;
        var lat2 = (double)latitude2.Value * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = ((double)longitude2.Value - (double)longitude1.Value) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

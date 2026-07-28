using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Domain.HotelNameMatching;
using Xunit;

namespace HotelMatchingPipeline.Domain.Tests;

public sealed class HotelPipelineDomainTests
{
    [Theory]
    [InlineData("CN", null, 3000, 200, 3000, 200)]
    [InlineData(null, "CHN", 3000, 200, 3000, 200)]
    [InlineData("US", null, 3000, 200, 300, null)]
    [InlineData(null, "DEU", 3000, 200, 300, null)]
    [InlineData(null, null, 3000, 200, 3000, 200)]
    public void Recall_policy_uses_a_small_unbounded_search_outside_china(
        string? regionLevel1Code,
        string? countryCode,
        int configuredRadius,
        int configuredMaxCandidates,
        int expectedRadius,
        int? expectedMaxCandidates)
    {
        var result = HotelRecallPolicy.Resolve(
            regionLevel1Code,
            countryCode,
            configuredRadius,
            configuredMaxCandidates);

        Assert.Equal(expectedRadius, result.RadiusMeters);
        Assert.Equal(expectedMaxCandidates, result.MaxCandidates);
    }

    [Fact]
    public void Cleaner_creates_valid_snapshot_for_consistent_data()
    {
        var cleaner = new HotelCleaner(new FixedRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-1")
        {
            SupplierId = 51,
            HotelName = "Shanghai Grand Hotel",
            CountryCode = "CHN",
            Telephone = "+86 21 5555 8888",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 121.4737m,
            Latitude = 31.2304m,
            SourceDataVersion = 3
        };

        var result = cleaner.Clean(raw, 7);

        Assert.True(result.IsMatchable);
        Assert.Equal(HotelDataQualityFlags.None, result.Quality.Flags);
        Assert.Equal("CHN", result.Cleaned.RegionLevel1Code);
        Assert.Contains("+862155558888", result.Cleaned.NormalizedPhones);
        var phone = Assert.Single(result.Cleaned.NormalizedPhoneDetails);
        Assert.Equal("+862155558888", phone.E164);
        Assert.Equal("86", phone.CountryCallingCode);
        Assert.Equal("CN", phone.RegionCode);
        Assert.True(phone.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(phone.NumberType));
        Assert.Equal(64, result.NormalizedDataHash.Length);
    }

    [Fact]
    public void Cleaner_marks_unknown_coordinate_type_as_problem()
    {
        var cleaner = new HotelCleaner(new FixedRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-2")
        {
            SupplierId = 51,
            HotelName = "Example Hotel",
            CountryCode = "CHN",
            Longitude = 121.4m,
            Latitude = 31.2m,
            GpsType = 0
        };

        var result = cleaner.Clean(raw, 1);

        Assert.False(result.IsMatchable);
        Assert.Equal(HotelDataQualityStatus.Problem, result.Quality.Status);
        Assert.True(result.Quality.Flags.HasFlag(HotelDataQualityFlags.CoordinateTypeUnknown));
    }

    [Fact]
    public void Cleaner_swaps_coordinates_when_only_swapped_order_matches_raw_country()
    {
        var cleaner = new HotelCleaner(new CoordinateOrderRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-SWAPPED")
        {
            SupplierId = 51,
            HotelName = "Coastal Hotel",
            CountryCode = "al",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 20.0125372m,
            Latitude = 39.8466451m
        };

        var result = cleaner.Clean(raw, 1);

        Assert.Equal(39.8466451m, result.Cleaned.Longitude);
        Assert.Equal(20.0125372m, result.Cleaned.Latitude);
        Assert.Equal("ALB", result.Cleaned.RegionLevel1Code);
        Assert.Equal("swapped_to_match_country", result.Quality.Details["coordinate_order"]);
        Assert.False(result.Quality.Flags.HasFlag(HotelDataQualityFlags.CoordinateInvalid));
    }

    [Fact]
    public void Cleaner_keeps_original_coordinates_when_swapped_order_does_not_match_raw_country()
    {
        var cleaner = new HotelCleaner(new CoordinateOrderRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-NOT-SWAPPED")
        {
            SupplierId = 51,
            HotelName = "Coastal Hotel",
            CountryCode = "US",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 20.0125372m,
            Latitude = 39.8466451m
        };

        var result = cleaner.Clean(raw, 1);

        Assert.Equal(20.0125372m, result.Cleaned.Longitude);
        Assert.Equal(39.8466451m, result.Cleaned.Latitude);
        Assert.Equal("GRC", result.Cleaned.RegionLevel1Code);
        Assert.DoesNotContain("coordinate_order", result.Quality.Details);
    }

    [Fact]
    public void Cleaner_can_recover_an_invalid_original_latitude_by_swapping_coordinates()
    {
        var cleaner = new HotelCleaner(new CoordinateOrderRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-INVALID-ORDER")
        {
            SupplierId = 51,
            HotelName = "Shanghai Hotel",
            CountryCode = "CN",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 31.2304m,
            Latitude = 121.4737m
        };

        var result = cleaner.Clean(raw, 1);

        Assert.Equal(121.4737m, result.Cleaned.Longitude);
        Assert.Equal(31.2304m, result.Cleaned.Latitude);
        Assert.Equal("CHN", result.Cleaned.RegionLevel1Code);
        Assert.False(result.Quality.Flags.HasFlag(HotelDataQualityFlags.CoordinateInvalid));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("123")]
    [InlineData("A1")]
    [InlineData("ALB1")]
    public void Cleaner_does_not_swap_coordinates_for_provider_specific_country_codes(string countryCode)
    {
        var cleaner = new HotelCleaner(new CoordinateOrderRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-PROVIDER-COUNTRY")
        {
            SupplierId = 51,
            HotelName = "Provider Hotel",
            CountryCode = countryCode,
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 20.0125372m,
            Latitude = 39.8466451m
        };

        var result = cleaner.Clean(raw, 1);

        Assert.Equal(20.0125372m, result.Cleaned.Longitude);
        Assert.Equal(39.8466451m, result.Cleaned.Latitude);
        Assert.Equal("GRC", result.Cleaned.RegionLevel1Code);
        Assert.DoesNotContain("coordinate_order", result.Quality.Details);
    }

    [Fact]
    public void Phone_normalizer_uses_resolved_country_for_toll_free_number_without_country_prefix()
    {
        var normalizer = new LibPhoneNumberNormalizer();
        var region = new RegionResolution("CHN", "China", null, null, null, null, RegionResolveStatus.Resolved, 1);

        var phone = Assert.Single(normalizer.Normalize("800 820 8820", region, null));

        Assert.True(phone.IsValid);
        Assert.Equal("+868008208820", phone.E164);
        Assert.Equal("86", phone.CountryCallingCode);
        Assert.Equal("CN", phone.RegionCode);
        Assert.Equal("TOLL_FREE", phone.NumberType);
    }

    [Fact]
    public void Phone_normalizer_fills_missing_country_calling_code_from_resolved_us_coordinates()
    {
        var normalizer = new LibPhoneNumberNormalizer();
        var region = new RegionResolution("USA", "United States", null, null, null, null, RegionResolveStatus.Resolved, 1);

        var phone = Assert.Single(normalizer.Normalize("(212) 555-0100", region, null));

        Assert.True(phone.IsValid);
        Assert.Equal("+12125550100", phone.E164);
        Assert.Equal("1", phone.CountryCallingCode);
        Assert.Equal("US", phone.RegionCode);
        Assert.Equal("212", phone.AreaCode);
    }

    [Fact]
    public void Phone_normalizer_keeps_explicit_country_calling_code_instead_of_coordinate_country()
    {
        var normalizer = new LibPhoneNumberNormalizer();
        var region = new RegionResolution("USA", "United States", null, null, null, null, RegionResolveStatus.Resolved, 1);

        var phone = Assert.Single(normalizer.Normalize("+44 20 7946 0958", region, null));

        Assert.True(phone.IsValid);
        Assert.Equal("+442079460958", phone.E164);
        Assert.Equal("44", phone.CountryCallingCode);
        Assert.Equal("GB", phone.RegionCode);
    }

    [Fact]
    public void Phone_normalizer_cleans_full_width_digits_spaces_and_punctuation()
    {
        var normalizer = new LibPhoneNumberNormalizer();
        var region = new RegionResolution("CHN", "China", null, null, null, null, RegionResolveStatus.Resolved, 1);

        var phone = Assert.Single(normalizer.Normalize(" 电话：＋８６ （２１） ５５５５－８８８８ ", region, null));

        Assert.True(phone.IsValid);
        Assert.Equal("+862155558888", phone.E164);
        Assert.Equal("86", phone.CountryCallingCode);
    }

    [Theory]
    [InlineData("0086 21 5555 8888")]
    [InlineData("++86 21 5555 8888")]
    public void Phone_normalizer_cleans_international_prefix_variants(string raw)
    {
        var normalizer = new LibPhoneNumberNormalizer();

        var phone = Assert.Single(normalizer.Normalize(raw, RegionResolution.NotFound(1), null));

        Assert.True(phone.IsValid);
        Assert.Equal("+862155558888", phone.E164);
    }

    [Theory]
    [InlineData("97142625555", "+97142625555")]
    [InlineData("97142387777", "+97142387777")]
    public void Phone_normalizer_recognizes_country_prefix_without_plus_when_coordinates_match(
        string raw,
        string expected)
    {
        var normalizer = new LibPhoneNumberNormalizer();
        var region = new RegionResolution("ARE", "United Arab Emirates", null, null, null, null, RegionResolveStatus.Resolved, 1);

        var phone = Assert.Single(normalizer.Normalize(raw, region, null));

        Assert.True(phone.IsValid);
        Assert.Equal(expected, phone.E164);
        Assert.Equal("971", phone.CountryCallingCode);
        Assert.Equal("AE", phone.RegionCode);
    }

    [Fact]
    public void Phone_normalizer_does_not_guess_country_prefix_without_country_context()
    {
        var normalizer = new LibPhoneNumberNormalizer();

        var phone = Assert.Single(normalizer.Normalize("97142625555", RegionResolution.NotFound(1), null));

        Assert.False(phone.IsValid);
        Assert.Equal("missing_default_region", phone.Error);
    }

    [Fact]
    public void Phone_normalizer_splits_multiple_explicit_numbers_separated_by_spaces()
    {
        var normalizer = new LibPhoneNumberNormalizer();

        var phones = normalizer.Normalize("+86 21 5555 8888  +1 212 555 0100", RegionResolution.NotFound(1), null);

        Assert.Equal(2, phones.Count);
        Assert.Contains(phones, phone => phone.E164 == "+862155558888");
        Assert.Contains(phones, phone => phone.E164 == "+12125550100");
    }

    [Fact]
    public void Country_codes_normalize_alpha2_and_alpha3_to_the_same_identity()
    {
        Assert.Equal("AD", CountryCodeNormalizer.ToAlpha2("AD"));
        Assert.Equal("AD", CountryCodeNormalizer.ToAlpha2("AND"));
        Assert.True(CountryCodeNormalizer.AreEquivalent("AD", "AND"));
        Assert.True(CountryCodeNormalizer.AreEquivalent("CHN", "CN"));
        Assert.True(CountryCodeNormalizer.AreEquivalentIsoCodes("al", "ALB"));
        Assert.True(CountryCodeNormalizer.AreEquivalentIsoCodes("chn", "CN"));
        Assert.False(CountryCodeNormalizer.AreEquivalentIsoCodes("123", "ALB"));
        Assert.False(CountryCodeNormalizer.AreEquivalentIsoCodes("A1", "AL"));
    }

    [Fact]
    public void Matcher_accepts_alpha2_and_alpha3_country_codes_for_the_same_hotel()
    {
        var supplier = CreateCleaned(
            HotelDataType.Supplier,
            "SUP-AD",
            "Andorra Hotel",
            1.5218m,
            42.5063m,
            51,
            phone: null,
            regionCode: "AND");
        var ota = CreateCleaned(
            HotelDataType.Platform,
            "MT-AD",
            "Andorra Hotel",
            1.5218m,
            42.5063m,
            phone: null,
            regionCode: "AD");

        var decision = new HotelMatchEvaluator().Evaluate(supplier, [ota], new Dictionary<string, IReadOnlyCollection<string>>());

        var candidate = Assert.Single(decision.Candidates);
        Assert.DoesNotContain("region_conflict", candidate.RejectionReasons);
    }

    [Fact]
    public void Cleaner_keeps_phone_country_when_it_conflicts_with_hotel_region()
    {
        var cleaner = new HotelCleaner(new FixedRegionResolver());
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-US")
        {
            SupplierId = 51,
            HotelName = "Example Hotel",
            CountryCode = "CHN",
            Telephone = "+1 212 555 0100",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 121.4737m,
            Latitude = 31.2304m
        };

        var result = cleaner.Clean(raw, 1);
        var phone = Assert.Single(result.Cleaned.NormalizedPhoneDetails);

        Assert.Equal("+12125550100", phone.E164);
        Assert.Equal("1", phone.CountryCallingCode);
        Assert.True(result.Quality.Flags.HasFlag(HotelDataQualityFlags.PhoneCountryConflict));
    }

    [Fact]
    public void Cleaner_passes_the_database_country_code_to_the_region_resolver()
    {
        var resolver = new PreferredCountryRegionResolver();
        var cleaner = new HotelCleaner(resolver);
        var raw = new RawHotelData(HotelDataType.Supplier, "SUP-PREFERRED-COUNTRY")
        {
            SupplierId = 51,
            HotelName = "Border Hotel",
            CountryCode = "CHN",
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 121.4737m,
            Latitude = 31.2304m
        };

        var result = cleaner.Clean(raw, 1);

        Assert.Equal("CHN", resolver.PreferredCountryCode);
        Assert.Equal("CHN", result.Cleaned.RegionLevel1Code);
    }

    [Fact]
    public void Matcher_auto_matches_close_valid_hotel()
    {
        var evaluator = new HotelMatchEvaluator();
        var supplier = CreateCleaned(HotelDataType.Supplier, "SUP-1", "Grand Hyatt Shanghai", 121.4737m, 31.2304m, 51);
        var ota = CreateCleaned(HotelDataType.Platform, "MT-1", "Shanghai Grand Hyatt", 121.4740m, 31.2305m);

        var decision = evaluator.Evaluate(supplier, [ota], new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Equal(HotelMatchDecisionKind.AutoMatched, decision.Kind);
        Assert.Equal("MT-1", decision.MTHotelCode);
    }

    [Fact]
    public void Matcher_sends_far_but_name_matching_hotel_to_review()
    {
        var evaluator = new HotelMatchEvaluator();
        var supplier = CreateCleaned(HotelDataType.Supplier, "SUP-1", "Grand Hyatt Shanghai", 121.4737m, 31.2304m, 51);
        var ota = CreateCleaned(HotelDataType.Platform, "MT-1", "Shanghai Grand Hyatt", 121.4900m, 31.2304m);

        var decision = evaluator.Evaluate(supplier, [ota], new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Equal(HotelMatchDecisionKind.NeedsReview, decision.Kind);
        Assert.Contains("outside_auto_radius", decision.Reasons);
    }

    [Fact]
    public void Matcher_requires_phone_between_300_meters_and_one_kilometer()
    {
        var evaluator = new HotelMatchEvaluator();
        var supplier = CreateCleaned(HotelDataType.Supplier, "SUP-1", "Grand Hyatt Shanghai", 121.4737m, 31.2304m, 51, null);
        var ota = CreateCleaned(HotelDataType.Platform, "MT-1", "Shanghai Grand Hyatt", 121.4790m, 31.2304m, phone: null);

        var decision = evaluator.Evaluate(supplier, [ota], new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Equal(HotelMatchDecisionKind.NoMatch, decision.Kind);
        Assert.Contains("phone_or_region_required_between_300m_and_1km", decision.Candidates[0].RejectionReasons);
    }

    [Fact]
    public void Matcher_sends_multiple_passing_candidates_to_review()
    {
        var evaluator = new HotelMatchEvaluator();
        var supplier = CreateCleaned(HotelDataType.Supplier, "SUP-1", "Grand Hyatt Shanghai", 121.4737m, 31.2304m, 51);
        var ota1 = CreateCleaned(HotelDataType.Platform, "MT-1", "Shanghai Grand Hyatt", 121.4740m, 31.2305m);
        var ota2 = CreateCleaned(HotelDataType.Platform, "MT-2", "Grand Hyatt Shanghai", 121.4742m, 31.2305m);

        var decision = evaluator.Evaluate(supplier, [ota1, ota2], new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Equal(HotelMatchDecisionKind.NeedsReview, decision.Kind);
        Assert.Contains("ambiguous_candidates", decision.Reasons);
    }

    [Fact]
    public void Matcher_rejects_problem_supplier_before_candidate_evaluation()
    {
        var evaluator = new HotelMatchEvaluator();
        var supplier = new CleanedHotel(HotelDataType.Supplier, "SUP-1", "Example", 51)
        {
            DataQualityStatus = HotelDataQualityStatus.Problem,
            DataQualityFlags = HotelDataQualityFlags.PhoneCountryConflict
        };

        var decision = evaluator.Evaluate(supplier, [], new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Equal(HotelMatchDecisionKind.DataProblem, decision.Kind);
    }

    [Fact]
    public void Name_matcher_supports_reordered_cjk_tokens()
    {
        var matcher = new HotelNameMatcher();
        var options = new HotelNameMatcherOptions
        {
            CjkDictionary = ["上海", "希尔顿"]
        };

        var result = matcher.Match("上海希尔顿", "希尔顿上海", options);

        Assert.True(result.IsMatch);
    }

    static CleanedHotel CreateCleaned(
        HotelDataType type,
        string code,
        string name,
        decimal longitude,
        decimal latitude,
        int? supplierId = null,
        string? phone = "+86 21 5555 8888",
        string regionCode = "CHN")
        => new(type, code, name, supplierId, type == HotelDataType.Platform ? code : null,
            telephone: phone,
            gpsType: (int)HotelCoordinateType.Wgs84,
            longitude: longitude,
            latitude: latitude)
        {
            RegionLevel1Code = regionCode,
            RegionLevel2Code = "ADM1-SH",
            RegionLevel3Code = "ADM2-SH",
            NormalizedPhones = phone is null ? [] : ["+862155558888"],
            DataQualityStatus = HotelDataQualityStatus.Valid,
            DataQualityFlags = HotelDataQualityFlags.None
        };

    sealed class FixedRegionResolver : IHotelRegionResolver
    {
        public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
            => new("CHN", "China", "ADM1-SH", "Shanghai", "ADM2-SH", "Shanghai", RegionResolveStatus.Resolved, 4);
    }

    sealed class CoordinateOrderRegionResolver : IHotelRegionResolver
    {
        public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
        {
            if (longitude == 20.0125372m && latitude == 39.8466451m)
            {
                return new("GRC", "Greece", null, null, null, null, RegionResolveStatus.Resolved, 4);
            }

            if (longitude == 39.8466451m && latitude == 20.0125372m)
            {
                return new("ALB", "Albania", null, null, null, null, RegionResolveStatus.Resolved, 4);
            }

            if (longitude == 121.4737m && latitude == 31.2304m)
            {
                return new("CHN", "China", null, null, null, null, RegionResolveStatus.Resolved, 4);
            }

            return RegionResolution.NotFound(4);
        }
    }

    sealed class PreferredCountryRegionResolver : IHotelRegionResolver
    {
        public string? PreferredCountryCode { get; private set; }

        public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The preferred-country overload must be used.");

        public RegionResolution Resolve(
            decimal longitude,
            decimal latitude,
            string? preferredCountryCode,
            CancellationToken cancellationToken = default)
        {
            PreferredCountryCode = preferredCountryCode;
            return new(preferredCountryCode, "Database Country", null, null, null, null, RegionResolveStatus.Resolved, 4);
        }
    }
}

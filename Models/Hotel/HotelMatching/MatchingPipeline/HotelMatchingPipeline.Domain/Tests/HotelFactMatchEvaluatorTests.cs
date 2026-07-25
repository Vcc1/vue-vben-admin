using HotelMatchingPipeline.Domain.Hotels;
using Xunit;

namespace HotelMatchingPipeline.Domain.Tests;

public sealed class HotelFactMatchEvaluatorTests
{
    [Fact]
    public void Uses_name_and_coordinate_evidence_from_different_fact_hotels()
    {
        var pending = Fact(
            HotelMatchFactSource.Supplier,
            1,
            hotelName: "A Hotel",
            longitude: 121.0000m,
            latitude: 31.0000m,
            starLevel: 5,
            address: "1 Test Road",
            phones: ["+8613800000000"]);
        var ota = Fact(
            HotelMatchFactSource.Ota,
            10,
            mtHotelCode: "MT-10",
            hotelName: "Different Name",
            longitude: 121.0002m,
            latitude: 31.0000m);
        var linkedSupplier = Fact(
            HotelMatchFactSource.Supplier,
            20,
            mtHotelCode: "MT-10",
            hotelName: "A",
            longitude: 122m,
            latitude: 32m,
            starLevel: 5,
            address: "1 Test Road",
            phones: ["+8613800000000"]);
        var workItem = new HotelMatchingWorkItem(
            1,
            pending,
            [ota],
            [],
            [linkedSupplier]);

        var result = new HotelFactMatchEvaluator().Evaluate(workItem, 60);

        var match = Assert.Single(result.Matches);
        Assert.Equal("MT-10", match.MTHotelCode);
        Assert.Equal("MT-10", result.ResolvedMTHotelCode);
        Assert.False(result.HasConflict);
        Assert.Equal(20, match.NameEvidence.HotelId);
        Assert.Equal(HotelMatchFactSource.Supplier, match.NameEvidence.Source);
        Assert.Equal(10, match.CoordinateEvidence.HotelId);
        Assert.Equal(HotelMatchFactSource.Ota, match.CoordinateEvidence.Source);
        Assert.InRange(match.CoordinateEvidence.DistanceMeters!.Value, 18, 20);
        Assert.Equal(20, match.StarEvidence!.HotelId);
        Assert.Equal(20, match.AddressEvidence!.HotelId);
        Assert.Equal(20, match.PhoneEvidence!.HotelId);
    }

    [Fact]
    public void Rejects_candidate_when_all_fact_coordinates_are_farther_than_sixty_meters()
    {
        var pending = Fact(
            HotelMatchFactSource.Supplier,
            1,
            hotelName: "A Hotel",
            longitude: 121m,
            latitude: 31m);
        var ota = Fact(
            HotelMatchFactSource.Ota,
            10,
            mtHotelCode: "MT-10",
            hotelName: "A Hotel",
            longitude: 121m,
            latitude: 31.0006m);
        var workItem = new HotelMatchingWorkItem(1, pending, [ota], [], []);

        var result = new HotelFactMatchEvaluator().Evaluate(workItem, 60);

        Assert.Empty(result.Matches);
        Assert.Equal(string.Empty, result.ResolvedMTHotelCode);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void Rejects_candidate_when_coordinate_matches_but_no_fact_name_matches()
    {
        var pending = Fact(
            HotelMatchFactSource.Supplier,
            1,
            hotelName: "A Hotel",
            longitude: 121m,
            latitude: 31m);
        var ota = Fact(
            HotelMatchFactSource.Ota,
            10,
            mtHotelCode: "MT-10",
            hotelName: "B Resort",
            longitude: 121.0001m,
            latitude: 31m);
        var workItem = new HotelMatchingWorkItem(1, pending, [ota], [], []);

        var result = new HotelFactMatchEvaluator().Evaluate(workItem, 60);

        Assert.Empty(result.Matches);
        Assert.Equal(string.Empty, result.ResolvedMTHotelCode);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void Multiple_candidates_produce_empty_main_mapping_and_conflict()
    {
        var pending = Fact(
            HotelMatchFactSource.Supplier,
            1,
            hotelName: "A Hotel",
            longitude: 121m,
            latitude: 31m);
        var firstOta = Fact(
            HotelMatchFactSource.Ota,
            10,
            mtHotelCode: "MT-10",
            hotelName: "A Hotel",
            longitude: 121.0001m,
            latitude: 31m);
        var secondOta = Fact(
            HotelMatchFactSource.Ota,
            11,
            mtHotelCode: "MT-11",
            hotelName: "A Hotel",
            longitude: 121.0002m,
            latitude: 31m);
        var workItem = new HotelMatchingWorkItem(
            1,
            pending,
            [firstOta, secondOta],
            [],
            []);

        var result = new HotelFactMatchEvaluator().Evaluate(workItem, 60);

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(string.Empty, result.ResolvedMTHotelCode);
        Assert.True(result.HasConflict);
    }

    static HotelMatchFact Fact(
        HotelMatchFactSource source,
        int hotelId,
        string? mtHotelCode = null,
        string? hotelName = null,
        decimal? longitude = null,
        decimal? latitude = null,
        decimal? starLevel = null,
        string? address = null,
        IReadOnlyList<string>? phones = null)
        => new(
            source,
            hotelId,
            source == HotelMatchFactSource.Supplier ? 51 : null,
            $"H-{hotelId}",
            mtHotelCode,
            hotelName,
            null,
            longitude,
            latitude,
            starLevel,
            address,
            phones ?? []);
}

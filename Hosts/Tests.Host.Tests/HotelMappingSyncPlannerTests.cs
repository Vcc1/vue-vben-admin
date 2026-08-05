using Tests.Host.HotelMappingSync;

namespace Tests.Host.Tests;

public sealed class HotelMappingSyncPlannerTests
{
    [Fact]
    public void BuildDesiredMappings_NormalizesValuesAndSkipsEmptyMtHotelCodes()
    {
        var rows = new[]
        {
            new HotelMappingSourceRow(51, " H100 ", " MT100 "),
            new HotelMappingSourceRow(51, "H200", " "),
            new HotelMappingSourceRow(32, "H300", "MT300")
        };

        var result = HotelMappingSyncPlanner.BuildDesiredMappings(rows);

        Assert.Equal(2, result.Count);
        Assert.Equal("MT100", result["51_H100"]);
        Assert.Equal("MT300", result["32_H300"]);
    }

    [Fact]
    public void BuildDesiredMappings_RejectsDuplicateNormalizedFields()
    {
        var rows = new[]
        {
            new HotelMappingSourceRow(51, "H100", "MT100"),
            new HotelMappingSourceRow(51, " H100 ", "MT101")
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => HotelMappingSyncPlanner.BuildDesiredMappings(rows));

        Assert.Contains("51_H100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlan_ReturnsExactAddsUpdatesAndDeletes()
    {
        var desired = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["51_A"] = "MT-A",
            ["51_B"] = "MT-B-NEW",
            ["51_SAME"] = "MT-SAME"
        };
        var current = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["51_B"] = "MT-B-OLD",
            ["51_C"] = "MT-C",
            ["51_SAME"] = "MT-SAME"
        };

        var plan = HotelMappingSyncPlanner.CreatePlan(desired, current);

        var added = Assert.Single(plan.Added);
        Assert.Equal("51_A", added.Field);
        Assert.Equal("MT-A", added.QueueValue);

        var updated = Assert.Single(plan.Updated);
        Assert.Equal("51_B", updated.Field);
        Assert.Equal("MT-B-NEW", updated.QueueValue);

        var deleted = Assert.Single(plan.Deleted);
        Assert.Equal("51_C", deleted.Field);
        Assert.Equal("MT-C", deleted.QueueValue);
    }
}

using Infrastructure.Redis.HotelMatchingRedis;
using System.Text.Json;
using Tests.Host.HotelMappingSync;

namespace Tests.Host.Tests;

public sealed class HotelMappingSyncPlannerTests
{
    [Fact]
    public void BuildDesiredMappings_NormalizesValuesAndSkipsEmptyMtHotelCodes()
    {
        var rows = new[]
        {
            new HotelMappingSourceRow(51, " H100 ", " MT100 ", " CN ", " SHA ", " 中文名 ", " English Name "),
            new HotelMappingSourceRow(51, "H200", " "),
            new HotelMappingSourceRow(32, "H300", "MT300")
        };

        var result = HotelMappingSyncPlanner.BuildDesiredMappings(rows);

        Assert.Equal(2, result.Count);
        var first = Assert.Single(result["MT100"]);
        Assert.Equal("MT100", first.MTHotelCode);
        Assert.Equal(51, first.SupId);
        Assert.Equal("CN", first.SContryCode);
        Assert.Equal("SHA", first.SCityCode);
        Assert.Equal("H100", first.SHotelCode);
        Assert.Equal("English Name", first.SHotelName);
        Assert.Equal(1, first.LV);
        Assert.Equal("H300", Assert.Single(result["MT300"]).SHotelCode);
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
        var desired = new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>(StringComparer.Ordinal)
        {
            ["MT-A"] = [Hotel("MT-A", 51, "A")],
            ["MT-B"] = [Hotel("MT-B", 51, "B"), Hotel("MT-B", 51, "B-NEW")],
            ["MT-SAME"] = [Hotel("MT-SAME", 32, "SAME"), Hotel("MT-SAME", 51, "SAME")]
        };
        var current = new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>(StringComparer.Ordinal)
        {
            ["MT-B"] = [Hotel("MT-B", 51, "B")],
            ["MT-C"] = [Hotel("MT-C", 51, "C")],
            ["MT-SAME"] = [Hotel("MT-SAME", 51, "SAME"), Hotel("MT-SAME", 32, "SAME")]
        };

        var plan = HotelMappingSyncPlanner.CreatePlan(desired, current);

        var added = Assert.Single(plan.Added);
        Assert.Equal("MT-A", added.MTHotelCode);
        Assert.Equal("MT-A", added.QueueValue);

        var updated = Assert.Single(plan.Updated);
        Assert.Equal("MT-B", updated.MTHotelCode);
        Assert.Equal("MT-B", updated.QueueValue);
        Assert.Equal(["B", "B-NEW"], updated.NewSupplierHotels!.Select(hotel => hotel.SHotelCode));

        var deleted = Assert.Single(plan.Deleted);
        Assert.Equal("MT-C", deleted.MTHotelCode);
        Assert.Equal("MT-C", deleted.QueueValue);
    }

    [Fact]
    public void CreatePlan_WhenSupplierHotelMoves_ChangesOldAndNewMtHotelGroups()
    {
        var desired = new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>(StringComparer.Ordinal)
        {
            ["MT-B"] = [Hotel("MT-B", 51, "H100")]
        };
        var current = new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>(StringComparer.Ordinal)
        {
            ["MT-A"] = [Hotel("MT-A", 51, "H100")]
        };

        var plan = HotelMappingSyncPlanner.CreatePlan(desired, current);

        Assert.Equal(["MT-B", "MT-A"],
            plan.Operations.Select(operation => operation.QueueValue));
        Assert.Single(plan.Added);
        Assert.Single(plan.Deleted);
        Assert.Empty(plan.Updated);
    }

    [Fact]
    public void BuildDesiredMappings_UsesChineseNameWhenEnglishNameIsEmpty()
    {
        var result = HotelMappingSyncPlanner.BuildDesiredMappings(
            [new HotelMappingSourceRow(51, "H100", "MT100", HotelName: " 中文名 ", HotelNameEn: " ")]);

        Assert.Equal("中文名", Assert.Single(result["MT100"]).SHotelName);
    }

    [Fact]
    public void SupHotelInfoDto_SerializesOnlyCompactRedisFields()
    {
        var json = JsonSerializer.Serialize(new SupHotelInfoDto
        {
            MTHotelCode = "MT100",
            SupId = 51,
            SContryCode = "CN",
            SCityCode = "SHA",
            SHotelCode = "H100",
            SHotelName = "Hotel Name",
            LV = 1
        });
        using var document = JsonDocument.Parse(json);

        Assert.Equal(["M", "I", "C1", "C2", "C3", "N", "LV"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    static SupHotelInfoDto Hotel(string mtHotelCode, int supId, string supplierHotelCode)
        => new()
        {
            MTHotelCode = mtHotelCode,
            SupId = supId,
            SHotelCode = supplierHotelCode
        };
}

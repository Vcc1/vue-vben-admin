using HotelPriceAvailability.Domain;
using Xunit;

namespace HotelPriceAvailability.Domain.Tests;

public sealed class PriceSearchPlanTests
{
    [Fact]
    public void Create_PreservesRuleDayAndPersonPriority()
    {
        var result = PriceSearchPlan.Create(
            new DateOnly(2026, 7, 21),
            [new PriceWindowRule(1, 1, 2), new PriceWindowRule(3, 1, 1)],
            [1, 2]);

        Assert.Collection(
            result,
            item => AssertCombination(item, "2026-07-22", "2026-07-23", 1, 0),
            item => AssertCombination(item, "2026-07-22", "2026-07-23", 2, 1),
            item => AssertCombination(item, "2026-07-23", "2026-07-24", 1, 2),
            item => AssertCombination(item, "2026-07-23", "2026-07-24", 2, 3),
            item => AssertCombination(item, "2026-07-22", "2026-07-25", 1, 4),
            item => AssertCombination(item, "2026-07-22", "2026-07-25", 2, 5));
    }

    [Fact]
    public void Create_RejectsDuplicateWindows()
    {
        var error = Assert.Throws<ArgumentException>(() => PriceSearchPlan.Create(
            new DateOnly(2026, 7, 21),
            [new PriceWindowRule(1, 1, 2), new PriceWindowRule(1, 2, 2)],
            [1]));

        Assert.Contains("Duplicate date window", error.Message);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 2, 1)]
    public void Create_RejectsInvalidRules(int stayDays, int fromDay, int toDay)
    {
        Assert.Throws<ArgumentException>(() => PriceSearchPlan.Create(
            new DateOnly(2026, 7, 21),
            [new PriceWindowRule(stayDays, fromDay, toDay)],
            [1]));
    }

    [Fact]
    public void SelectPreferred_UsesConfiguredPriorityInsteadOfCompletionOrder()
    {
        var combinations = PriceSearchPlan.Create(
            new DateOnly(2026, 7, 21),
            [new PriceWindowRule(1, 1, 2)],
            [1]);

        var later = new PriceHit(7, combinations[1]);
        var earlier = new PriceHit(7, combinations[0]);

        Assert.Equal(earlier, PriceHit.SelectPreferred(later, earlier));
        Assert.Equal(earlier, PriceHit.SelectPreferred(earlier, later));
    }

    private static void AssertCombination(
        PriceSearchCombination combination,
        string checkIn,
        string checkOut,
        int adults,
        int priority)
    {
        Assert.Equal(DateOnly.Parse(checkIn), combination.Window.CheckIn);
        Assert.Equal(DateOnly.Parse(checkOut), combination.Window.CheckOut);
        Assert.Equal(adults, combination.Adults);
        Assert.Equal(priority, combination.Priority);
    }
}

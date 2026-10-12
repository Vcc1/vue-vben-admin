namespace HotelPriceAvailability.Domain;

public sealed record PriceWindowRule(int StayDays, int FromDay, int ToDay);

public sealed record PriceSearchWindow(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int RuleOrder,
    int DayOrder);

public sealed record PriceSearchCombination(
    PriceSearchWindow Window,
    int Adults,
    int PersonOrder,
    int Priority)
{
    public string PriceTime => $"{Window.CheckIn:yyyy-MM-dd}~{Window.CheckOut:yyyy-MM-dd}";
}

public static class PriceSearchPlan
{
    public static IReadOnlyList<PriceSearchCombination> Create(
        DateOnly baseDate,
        IReadOnlyList<PriceWindowRule> rules,
        IReadOnlyList<int> persons)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(persons);

        if (rules.Count == 0)
        {
            throw new ArgumentException("At least one date window rule is required.", nameof(rules));
        }

        if (persons.Count == 0)
        {
            throw new ArgumentException("At least one adult count is required.", nameof(persons));
        }

        if (persons.Any(static value => value <= 0) || persons.Distinct().Count() != persons.Count)
        {
            throw new ArgumentException("Adult counts must be positive and unique.", nameof(persons));
        }

        var windows = new List<PriceSearchWindow>();
        var uniqueWindows = new HashSet<(DateOnly CheckIn, DateOnly CheckOut)>();
        for (var ruleOrder = 0; ruleOrder < rules.Count; ruleOrder++)
        {
            var rule = rules[ruleOrder];
            if (rule.StayDays <= 0 || rule.FromDay < 0 || rule.ToDay < rule.FromDay)
            {
                throw new ArgumentException(
                    $"Invalid date window rule at index {ruleOrder}: stayDays={rule.StayDays}, fromDay={rule.FromDay}, toDay={rule.ToDay}.",
                    nameof(rules));
            }

            for (var day = rule.FromDay; day <= rule.ToDay; day++)
            {
                var checkIn = baseDate.AddDays(day);
                var checkOut = checkIn.AddDays(rule.StayDays);
                if (!uniqueWindows.Add((checkIn, checkOut)))
                {
                    throw new ArgumentException(
                        $"Duplicate date window: {checkIn:yyyy-MM-dd}~{checkOut:yyyy-MM-dd}.",
                        nameof(rules));
                }

                windows.Add(new PriceSearchWindow(checkIn, checkOut, ruleOrder, day - rule.FromDay));
            }
        }

        var result = new List<PriceSearchCombination>(windows.Count * persons.Count);
        var priority = 0;
        foreach (var window in windows)
        {
            for (var personOrder = 0; personOrder < persons.Count; personOrder++)
            {
                result.Add(new PriceSearchCombination(
                    window,
                    persons[personOrder],
                    personOrder,
                    priority++));
            }
        }

        return result;
    }
}

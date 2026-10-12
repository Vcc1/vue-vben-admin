namespace HotelPriceAvailability.Domain;

public sealed record PriceHit(int TaskId, PriceSearchCombination Combination)
{
    public string PriceTime => Combination.PriceTime;

    public static PriceHit SelectPreferred(PriceHit current, PriceHit candidate)
    {
        if (current.TaskId != candidate.TaskId)
        {
            throw new ArgumentException("Price hits must belong to the same task.", nameof(candidate));
        }

        return candidate.Combination.Priority < current.Combination.Priority ? candidate : current;
    }
}

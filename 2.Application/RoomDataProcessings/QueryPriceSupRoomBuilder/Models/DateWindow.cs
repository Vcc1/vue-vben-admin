namespace QueryPriceSupRoomBuilder;

public readonly record struct DateWindow(DateTime CheckIn, DateTime CheckOut)
{
    public string In => CheckIn.ToString("yyyy-MM-dd");
    public string Out => CheckOut.ToString("yyyy-MM-dd");
}

public readonly record struct DateWindowRule(int StayNights, int FromDayOffset, int ToDayOffset)
{
    public IEnumerable<DateWindow> Build(DateTime baseDate)
    {
        for (var offset = FromDayOffset; offset <= ToDayOffset; offset++)
        {
            var checkIn = baseDate.Date.AddDays(offset);
            yield return new DateWindow(checkIn, checkIn.AddDays(StayNights));
        }
    }
}

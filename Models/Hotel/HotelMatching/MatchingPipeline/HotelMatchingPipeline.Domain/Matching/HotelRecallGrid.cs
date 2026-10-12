namespace HotelMatchingPipeline.Domain.Hotels;

public readonly record struct HotelRecallGridRange(long Minimum, long Maximum);

public static class HotelRecallGrid
{
    public const int CellsPerDegree = 2_000;
    public const int LongitudeCellCount = 360 * CellsPerDegree + 1;

    const double MinimumMetersPerLatitudeDegree = 110_000;
    const double ConservativeMetersPerLongitudeDegree = 111_000;

    public static IReadOnlyList<HotelRecallGridRange> GetRanges(
        decimal latitude,
        decimal longitude,
        int radiusMeters)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }
        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusMeters);

        var latitudeValue = (double)latitude;
        var longitudeValue = (double)longitude;
        var latitudeDelta = radiusMeters / MinimumMetersPerLatitudeDegree;
        var minimumLatitude = Math.Max(-90, latitudeValue - latitudeDelta);
        var maximumLatitude = Math.Min(90, latitudeValue + latitudeDelta);
        var longitudeIntervals = GetLongitudeIntervals(
            latitudeValue,
            longitudeValue,
            latitudeDelta,
            radiusMeters);
        var minimumLatitudeCell = LatitudeCell(minimumLatitude);
        var maximumLatitudeCell = LatitudeCell(maximumLatitude);
        var ranges = new List<HotelRecallGridRange>(
            (maximumLatitudeCell - minimumLatitudeCell + 1) * longitudeIntervals.Count);

        for (var latitudeCell = minimumLatitudeCell;
             latitudeCell <= maximumLatitudeCell;
             latitudeCell++)
        {
            var rowOffset = (long)latitudeCell * LongitudeCellCount;
            foreach (var interval in longitudeIntervals)
            {
                ranges.Add(new HotelRecallGridRange(
                    rowOffset + LongitudeCell(interval.Minimum),
                    rowOffset + LongitudeCell(interval.Maximum)));
            }
        }

        return ranges;
    }

    public static long GetGridId(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }
        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        return (long)LatitudeCell(latitude) * LongitudeCellCount
            + LongitudeCell(longitude);
    }

    static IReadOnlyList<LongitudeInterval> GetLongitudeIntervals(
        double latitude,
        double longitude,
        double latitudeDelta,
        int radiusMeters)
    {
        var maximumAbsoluteLatitude = Math.Min(
            90,
            Math.Max(
                Math.Abs(latitude - latitudeDelta),
                Math.Abs(latitude + latitudeDelta)));
        var cosine = Math.Cos(maximumAbsoluteLatitude * Math.PI / 180);
        if (cosine <= 0
            || radiusMeters >= ConservativeMetersPerLongitudeDegree * cosine * 180)
        {
            return [new(-180, 180)];
        }

        var longitudeDelta = Math.Min(
            180,
            radiusMeters / (ConservativeMetersPerLongitudeDegree * cosine));
        var minimumLongitude = longitude - longitudeDelta;
        var maximumLongitude = longitude + longitudeDelta;
        if (minimumLongitude < -180)
        {
            return
            [
                new(-180, maximumLongitude),
                new(minimumLongitude + 360, 180)
            ];
        }
        if (maximumLongitude > 180)
        {
            return
            [
                new(-180, maximumLongitude - 360),
                new(minimumLongitude, 180)
            ];
        }

        return [new(minimumLongitude, maximumLongitude)];
    }

    static int LatitudeCell(double latitude) =>
        Math.Clamp(
            (int)Math.Floor((latitude + 90) * CellsPerDegree),
            0,
            180 * CellsPerDegree);

    static int LatitudeCell(decimal latitude) =>
        Math.Clamp(
            (int)decimal.Floor((latitude + 90) * CellsPerDegree),
            0,
            180 * CellsPerDegree);

    static int LongitudeCell(double longitude) =>
        Math.Clamp(
            (int)Math.Floor((longitude + 180) * CellsPerDegree),
            0,
            LongitudeCellCount - 1);

    static int LongitudeCell(decimal longitude) =>
        Math.Clamp(
            (int)decimal.Floor((longitude + 180) * CellsPerDegree),
            0,
            LongitudeCellCount - 1);

    readonly record struct LongitudeInterval(double Minimum, double Maximum);
}

namespace HotelMatchingPipeline.Domain.Hotels;

public enum HotelCoordinateType
{
    Unknown = 0,
    Wgs84 = 1,
    Gcj02 = 2,
    Bd09 = 3
}

public sealed record NormalizedCoordinate(
    decimal? Longitude,
    decimal? Latitude,
    HotelCoordinateType Type,
    bool IsValid,
    string? Error)
{
    public static NormalizedCoordinate Missing(string error = "missing")
        => new(null, null, HotelCoordinateType.Unknown, false, error);
}

public static class HotelCoordinateNormalizer
{
    const double Pi = Math.PI;
    const double A = 6378245.0;
    const double Ee = 0.00669342162296594323;

    public static NormalizedCoordinate Normalize(decimal? longitude, decimal? latitude, int? type)
    {
        if (!longitude.HasValue || !latitude.HasValue)
        {
            return NormalizedCoordinate.Missing();
        }

        var lon = (double)longitude.Value;
        var lat = (double)latitude.Value;
        var coordinateType = (HotelCoordinateType)(type ?? 0);
        if (coordinateType == HotelCoordinateType.Unknown)
        {
            return new NormalizedCoordinate(null, null, coordinateType, false, "unknown_coordinate_type");
        }

        if (Math.Abs(lon) > 180 || Math.Abs(lat) > 90 || (Math.Abs(lon) < 0.000001 && Math.Abs(lat) < 0.000001))
        {
            return new NormalizedCoordinate(null, null, coordinateType, false, "out_of_range");
        }

        if (coordinateType == HotelCoordinateType.Bd09)
        {
            (lon, lat) = Bd09ToGcj02(lon, lat);
            coordinateType = HotelCoordinateType.Gcj02;
        }

        if (coordinateType == HotelCoordinateType.Gcj02)
        {
            (lon, lat) = Gcj02ToWgs84(lon, lat);
        }

        return new NormalizedCoordinate((decimal)lon, (decimal)lat, HotelCoordinateType.Wgs84, true, null);
    }

    static (double Longitude, double Latitude) Bd09ToGcj02(double longitude, double latitude)
    {
        var x = longitude - 0.0065;
        var y = latitude - 0.006;
        var z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * Pi);
        var theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * Pi);
        return (z * Math.Cos(theta), z * Math.Sin(theta));
    }

    static (double Longitude, double Latitude) Gcj02ToWgs84(double longitude, double latitude)
    {
        var (deltaLon, deltaLat) = Transform(longitude, latitude);
        return (longitude * 2 - (longitude + deltaLon), latitude * 2 - (latitude + deltaLat));
    }

    static (double Longitude, double Latitude) Transform(double longitude, double latitude)
    {
        var dLat = TransformLatitude(longitude - 105.0, latitude - 35.0);
        var dLon = TransformLongitude(longitude - 105.0, latitude - 35.0);
        var radLat = latitude / 180.0 * Pi;
        var magic = 1 - Ee * Math.Sin(radLat) * Math.Sin(radLat);
        var sqrtMagic = Math.Sqrt(magic);
        dLat = dLat * 180.0 / (A * (1 - Ee) / (magic * sqrtMagic) * Pi);
        dLon = dLon * 180.0 / (A / sqrtMagic * Math.Cos(radLat) * Pi);
        return (dLon, dLat);
    }

    static double TransformLatitude(double x, double y)
        => -100 + 2 * x + 3 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x))
            + (20 * Math.Sin(6 * x * Pi) + 20 * Math.Sin(2 * x * Pi)) * 2 / 3
            + (20 * Math.Sin(y * Pi) + 40 * Math.Sin(y / 3 * Pi)) * 2 / 3
            + (160 * Math.Sin(y / 12 * Pi) + 320 * Math.Sin(y * Pi / 30)) * 2 / 3;

    static double TransformLongitude(double x, double y)
        => 300 + x + 2 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x))
            + (20 * Math.Sin(6 * x * Pi) + 20 * Math.Sin(2 * x * Pi)) * 2 / 3
            + (20 * Math.Sin(x * Pi) + 40 * Math.Sin(x / 3 * Pi)) * 2 / 3
            + (150 * Math.Sin(x / 12 * Pi) + 300 * Math.Sin(x / 30 * Pi)) * 2 / 3;
}

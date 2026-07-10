using System.Diagnostics.Metrics;
using VBase.AppMetrics;

namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataMetrics
{
    public const string ScopeName = "SupplierStaticData";

    readonly Counter<long>? _lookupCounter;
    readonly Histogram<double>? _lookupDurationMs;
    readonly Histogram<long>? _roomCount;

    public SupplierStaticDataMetrics()
    {
    }

    public SupplierStaticDataMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;
        _lookupCounter = meter.CreateCounter<long>("supplier_static_data_lookup_total");
        _lookupDurationMs = meter.CreateHistogram<double>("supplier_static_data_lookup_duration_ms");
        _roomCount = meter.CreateHistogram<long>("supplier_static_data_room_count");
    }

    public void RecordLookup(
        int supId,
        SupplierStaticDataKind dataKind,
        SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> result,
        TimeSpan elapsed)
    {
        var source = NormalizeSource(result.Source);
        var outcome = GetOutcome(result);
        var roomCount = GetRoomCount(result);

        _lookupCounter?.Add(
            1,
            Tag("sup_id", supId.ToString()),
            Tag("data_kind", FormatDataKind(dataKind)),
            Tag("outcome", outcome),
            Tag("source", source),
            Tag("error_code", NormalizeErrorCode(result.ErrorCode)));

        _lookupDurationMs?.Record(
            Math.Max(0, elapsed.TotalMilliseconds),
            Tag("sup_id", supId.ToString()),
            Tag("data_kind", FormatDataKind(dataKind)),
            Tag("outcome", outcome),
            Tag("source", source));

        if (result.Success)
        {
            _roomCount?.Record(
                roomCount,
                Tag("sup_id", supId.ToString()),
                Tag("data_kind", FormatDataKind(dataKind)),
                Tag("source", source));
        }
    }

    public void RecordException(
        int supId,
        SupplierStaticDataKind dataKind,
        TimeSpan elapsed)
    {
        _lookupCounter?.Add(
            1,
            Tag("sup_id", supId.ToString()),
            Tag("data_kind", FormatDataKind(dataKind)),
            Tag("outcome", "failed"),
            Tag("source", "unknown"),
            Tag("error_code", "exception"));

        _lookupDurationMs?.Record(
            Math.Max(0, elapsed.TotalMilliseconds),
            Tag("sup_id", supId.ToString()),
            Tag("data_kind", FormatDataKind(dataKind)),
            Tag("outcome", "failed"),
            Tag("source", "unknown"));
    }

    public static string FormatDataKind(SupplierStaticDataKind dataKind)
    {
        return dataKind switch
        {
            SupplierStaticDataKind.Hotel => "hotel",
            SupplierStaticDataKind.Room => "room",
            SupplierStaticDataKind.HotelAndRoom => "hotelAndRoom",
            _ => dataKind.ToString()
        };
    }

    public static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "unknown";
        }

        return source.Trim().ToLowerInvariant() switch
        {
            "cache" => "cache",
            "direct" => "direct",
            "mixed" => "mixed",
            _ => "unknown"
        };
    }

    public static string NormalizeErrorCode(string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode) ? "none" : errorCode.Trim();
    }

    public static long GetRoomCount(SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> result)
    {
        return result.Data?.Rooms?.Count ?? 0;
    }

    public static string GetOutcome(SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> result)
    {
        if (!result.Success)
        {
            return "failed";
        }

        return GetRoomCount(result) > 0
            ? "success_with_rooms"
            : "success_without_rooms";
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }

}

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QueryPriceSupRoomBuilder;

public sealed class SupRoomCandidateSelector
{
    static readonly Regex ReadableEnglishRoomName = new(
        @"^[\p{IsBasicLatin}]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly JsonSerializerOptions BedInfoJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public IReadOnlyList<SupRoomRedisMessage> Select(
        IEnumerable<QueryPriceRoom> prices,
        int supId,
        RoomBuildTaskMetrics metrics)
    {
        var rooms = new List<SupRoomRedisMessage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var price in prices)
        {
            var room = Map(price, supId);
            var reason = GetFilterReason(room);
            if (reason is not null)
            {
                metrics.RecordFiltered(supId, reason);
                continue;
            }

            var key = BuildDedupKey(room);
            if (!seen.Add(key))
            {
                metrics.RecordFiltered(supId, "duplicate");
                continue;
            }

            rooms.Add(room);
        }

        return rooms;
    }

    static SupRoomRedisMessage Map(QueryPriceRoom price, int supId)
    {
        return new SupRoomRedisMessage
        {
            RoomName = TrimToNull(price.SupRoomName),
            RoomEnName = TrimToNull(price.SupRoomEnName),
            HotelCode = TrimToNull(price.SupHotelCode),
            MTHotelCode = TrimToNull(price.MTHotelCode),
            RoomCode = TrimToNull(price.SupRoomCode),
            BedType = null,
            BedInfo = BuildBedInfo(price),
            BedInfoKey = TrimToNull(price.BedInfoKey),
            AcreageFrom = 0,
            AcreageTo = 0,
            SmokeType = 0,
            MaxAdt = Math.Max(0, price.Person),
            SupId = supId,
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    static string? BuildBedInfo(QueryPriceRoom price)
    {
        if (price.MTBeds?.Any(static group => group.Beds.Count > 0) == true)
        {
            return JsonSerializer.Serialize(price.MTBeds, BedInfoJsonOptions);
        }

        return TrimToNull(price.SupBedInfo);
    }

    static string? GetFilterReason(SupRoomRedisMessage room)
    {
        if (!HasReadableRoomName(room))
        {
            return "room_name_not_readable";
        }

        if (IsTooLong(room.RoomName, 200)
            || IsTooLong(room.RoomEnName, 200)
            || IsTooLong(room.RoomCode, 100)
            || IsTooLong(room.HotelCode, 100)
            || IsTooLong(room.BedInfoKey, 200))
        {
            return "field_too_long";
        }

        if (string.IsNullOrWhiteSpace(room.MTHotelCode)
            || string.IsNullOrWhiteSpace(room.HotelCode)
            || string.IsNullOrWhiteSpace(room.RoomCode))
        {
            return "required_field_empty";
        }

        return null;
    }

    static bool HasReadableRoomName(SupRoomRedisMessage room)
    {
        return IsReadableEnglish(room.RoomEnName) || IsReadableEnglish(room.RoomName);
    }

    static bool IsReadableEnglish(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && ReadableEnglishRoomName.IsMatch(value)
            && value.Any(char.IsLetterOrDigit);
    }

    static bool IsTooLong(string? value, int maxLength)
    {
        return value?.Length > maxLength;
    }

    static string BuildDedupKey(SupRoomRedisMessage room)
    {
        return string.Join(
            '\u001f',
            room.HotelCode,
            room.MTHotelCode,
            room.RoomCode,
            room.RoomName,
            room.RoomEnName,
            room.BedInfo,
            room.BedInfoKey,
            room.AcreageFrom,
            room.AcreageTo,
            room.SmokeType,
            room.MaxAdt,
            room.SupId);
    }

    static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

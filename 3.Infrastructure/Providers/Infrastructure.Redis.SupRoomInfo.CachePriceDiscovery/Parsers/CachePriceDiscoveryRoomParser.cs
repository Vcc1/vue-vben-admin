namespace Infrastructure.Redis.SupRoomInfo.CachePriceDiscovery.Parsers;

public static class CachePriceDiscoveryRoomParser
{
    const string Separator = "^^";

    public static bool TryParse(string? value, out Models.CachePriceDiscoveryRoomModel? model)
    {
        model = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var firstIndex = value.IndexOf(Separator, StringComparison.Ordinal);
        if (firstIndex < 0)
        {
            return false;
        }

        var secondIndex = value.IndexOf(Separator, firstIndex + Separator.Length, StringComparison.Ordinal);
        if (secondIndex < 0)
        {
            return false;
        }

        var lastIndex = value.LastIndexOf(Separator, StringComparison.Ordinal);
        if (lastIndex <= secondIndex)
        {
            return false;
        }

        var mtHotelCode = value[..firstIndex];
        var supHotelCode = value.Substring(firstIndex + Separator.Length, secondIndex - firstIndex - Separator.Length);
        var roomName = value.Substring(secondIndex + Separator.Length, lastIndex - secondIndex - Separator.Length);
        var bedInfoKey = value[(lastIndex + Separator.Length)..];

        if (string.IsNullOrWhiteSpace(mtHotelCode)
            || string.IsNullOrWhiteSpace(supHotelCode)
            || string.IsNullOrWhiteSpace(roomName)
            )
        {
            return false;
        }

        model = new Models.CachePriceDiscoveryRoomModel
        {
            MTHotelCode = mtHotelCode,
            SupHotelCode = supHotelCode,
            RoomName = roomName,
            BedInfoKey = bedInfoKey,
        };

        return true;
    }
}

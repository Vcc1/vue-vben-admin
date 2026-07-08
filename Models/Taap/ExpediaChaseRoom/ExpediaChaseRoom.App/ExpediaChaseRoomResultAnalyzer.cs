using System.Text.Json;

namespace ExpediaChaseRoomApp;

public static class ExpediaChaseRoomResultAnalyzer
{
    private const string OfflineMessagePrefix = "The following property was excluded by Klopp filter propertyId:";

    public static bool IsOffline(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return false;
        }

        return rawContent.Contains(OfflineMessagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasPrice(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return false;
        }

        using var document = JsonDocument.Parse(rawContent);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("data", out var data))
            {
                continue;
            }

            if (!data.TryGetProperty("propertyOffers", out var propertyOffers))
            {
                continue;
            }

            if (!propertyOffers.TryGetProperty("categorizedListings", out var categorizedListings))
            {
                continue;
            }

            if (categorizedListings.ValueKind == JsonValueKind.Array && categorizedListings.GetArrayLength() > 0)
            {
                return true;
            }
        }

        return false;
    }
}
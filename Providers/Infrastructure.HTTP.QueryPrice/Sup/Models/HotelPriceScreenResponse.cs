using System.Text.Json;
using System.Text.Json.Serialization;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.HTTP.QueryPrice.Sup.Models;

internal sealed class HotelPriceScreenResponse
{
    public int? Code { get; set; }
    public string? Msg { get; set; }
    public List<PriceResult> ListPrice { get; set; } = [];
}

internal sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDecimal().ToString(),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            _ => JsonDocument.ParseValue(ref reader).RootElement.ToString()
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

internal sealed class PriceResult
{
    public string? SupId { get; set; }
    public string? MTHotelCode { get; set; }
    public string? SupplierName { get; set; }
    public string? SupRoomName { get; set; }
    public string? SupRoomEnName { get; set; }
    public string? SupRoomCode { get; set; }
    public string? SupHotelCode { get; set; }
    public string? SupBedInfo { get; set; }
    public List<BedGroup>? MTBeds { get; set; }
    public string? BedInfoKey { get; set; }
    public string[]? PriceTags { get; set; }
    public JsonElement? CancellationPolicy { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? LastFreeCancelDate { get; set; }
    public int Person { get; set; }
    public bool IsBreakfast { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public decimal SupTotalPrice { get; set; }
    public decimal SupTotalPriceRMB { get; set; }
}

namespace Infrastructure.HTTP.OTAHotelInfo;

public sealed class OtaHotelInfoApiException : Exception
{
    public OtaHotelInfoApiException(int resultCode, string? resultMessage)
        : base($"OTA hotel information query failed. ResultCode={resultCode}, ResultMessage={resultMessage}")
    {
        ResultCode = resultCode;
        ResultMessage = resultMessage;
    }

    public int ResultCode { get; }

    public string? ResultMessage { get; }
}

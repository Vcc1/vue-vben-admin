namespace Infrastructure.HTTP.OTAHotelInfo;

public sealed class OtaHotelInfoOptions
{
    public string Endpoint { get; set; } = "http://118.31.20.238:8985/api/Hotel/GetHotelPageInfo";

    public string UserName { get; set; } = "YD";

    public string Md5Key { get; set; } = "B35E0F556A8AB12F58E26C3930B7A27F";

    public int TimeoutSeconds { get; set; } = 120;
}

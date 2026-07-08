namespace Infrastructure.HTTP.QueryPrice.Sup.Models;

internal sealed class RoomMatchRequest
{
    public RoomMatchTypeChoose RoomMatchTypeChoose { get; set; } = new();
    public string ApiKey { get; set; } = "";
    public string Gid { get; set; } = "";
    public string SupId { get; set; } = "";
    public string RequestFrom { get; set; } = "OTANew";
    public string ReqSource { get; set; } = "1001";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public IReadOnlyList<string> ListMTHotelCode { get; set; } = [];
    public List<ListRoom> ListRoom { get; set; } = [];
    public int MTNationalityId { get; set; } = 74;
    public int TimeOut { get; set; } = 120000;
}

internal sealed class RoomMatchTypeChoose
{
    public string RoomSource { get; set; } = "115";
    public string IsMustAuditRoom { get; set; } = "N";
}

internal sealed class ListRoom
{
    public int ChildNumber { get; set; }
    public int AdultNumber { get; set; }
    public List<ListPerson> ListPerson { get; set; } = [];
}

internal sealed class ListPerson
{
    public int RoomIndex { get; set; } = 1;
    public int Type { get; set; } = 1;
    public int Age { get; set; } = 30;
    public string FirstName { get; set; } = "TBA";
    public string LastName { get; set; } = "TBA";
}

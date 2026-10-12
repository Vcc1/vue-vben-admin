using MTOTA.Entity.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("AgodaHotelInfo")]
    public class AgodaHotelInfo
    {
        [Key]
        public int Id { get; set; }
        public PlatForm PlatFormID { get; set; }
        public string MTHotelCode { get; set; }
        public string CountryEnName { get; set; }
        public string CountryName { get; set; }
        public string CityName { get; set; }
        public string CityEnName { get; set; }
        public string HotelEnName { get; set; }
        public string HotelAddress { get; set; }
        public string HotelEnAddress { get; set; }
        public string Longitude { get; set; }
        public string Latitude { get; set; }
        public int? OnLineStatus { get; set; }
        public string HotelName { get; set; }


        public bool EnabledMinLvRoomAddPrice { get; set; }
        public bool EnabledCanCancel { get; set; }
        public decimal AddPriceMaxMultiple { get; set; }
        public bool EnableNBFToBF { get; set; }
        public bool EnableNoRoomAddPrice { get; set; }
        public bool EnableRoomLevel { get; set; }
        public bool PlatIsOpen { get; set; }
        public string ManagerUser { get; set; }
        public int CloseSellDay { get; set; }
        public bool IsMatch { get; set; }
        public int PushStatus { get; set; }
        public bool? IsEnableFixPrice { get; set; }
        public DateTime CreateTime { get; set; }
        public int IsPullRoom { get; set; }
        public string BatchNo { get; set; }
        public decimal AddPriceValue { get; set; }
        public int AddPriceType { get; set; }
        public int? HotelStar { get; set; }
        public string HotelTel { get; set; }
        public string PostalCode { get; set; }


        public int MaxRoomNum { get; set; }
        public string CountryCode { get; set; }
    }
}
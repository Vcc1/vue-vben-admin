using MTOTA.Entity.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("OTAHotel")]
    public class OTAHotel
    {
        [Key]
        public int Id { get; set; }
        //public PlatForm PlatFormID { get; set; }
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
        public string HotelName { get; set; }
        public string HotelTel { get; set; }
        public string PostalCode { get; set; }
        public bool CtripIsMatch { get; set; }
        public DateTime? CtripMatchTime { get; set; }
        public string StarLevel { get; set; }
        public string CountryCode { get; set; }
    }
}
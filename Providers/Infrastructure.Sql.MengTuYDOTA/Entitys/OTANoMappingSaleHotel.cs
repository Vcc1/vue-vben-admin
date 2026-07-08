using MTOTA.Entity.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("OTANoMappingSale_Hotel")]
    public class OTANoMappingSaleHotel
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
        public int CloseSellDay { get; set; }
        public int PushStatus { get; set; }
        public decimal AddPriceValue { get; set; }
        public int AddPriceType { get; set; }
        public int? HotelStar { get; set; }
        public string HotelTel { get; set; }
        public string PostalCode { get; set; }
        public string CountryCode { get; set; }

        public DateTime PushTime { get; set; }

        public int SupId { get; set; }
        public string SupFlag { get; set; }
        public string SHotelCode { get; set; }
        public string SCityCode { get; set; }
        public string SContryCode { get; set; }
        public int SupHotelId { get; set; }
    }
}

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
    [Table("SubRoomPricePlan")]
    public class SubRoomPricePlan
    {
        [Key]
        public int ID { get; set; }
        public PlatForm PlatFormID { get; set; }
        public int MasterHotelID { get; set; }
        public int SubHotelID { get; set; }
        public int RoomId { get; set; }
        public int BasicRoomTypeId { get; set; }
        public int MasterBasicRoomTypeId { get; set; }
        public string RoomName { get; set; }
        public string ExtraBedFee { get; set; }
        public int MaxAudltOccupancy { get; set; }
        public int MaxChildrenOccupancy { get; set; }
        public int Smoking { get; set; }
        public int Applicability { get; set; }
        public string OtherDescription { get; set; }
        public string HasBroadnet { get; set; }
        public string HasWirelessBroadnet { get; set; }
        public string WirelessBroadnetRoom { get; set; }
        public string WirelessBroadnetFee { get; set; }
        public string HasWiredBroadnet { get; set; }
        public string WiredBroadnetRoom { get; set; }
        public string WiredBroadnetFee { get; set; }
        public string BroadnetFeeDetail { get; set; }
        public string RoomTypeName { get; set; }
        public string BedInfo { get; set; }
        public int RoomStatus { get; set; }
        public string RateCodePropertyValueIDList { get; set; }
        public string MealCount { get; set; }
        public string ApplicabilityInfo { get; set; }
        public string ApplicabilityList { get; set; }
        public string RoomEnName { get; set; }
        public int MealProperty { get; set; }
        public int RoomNumberProperty { get; set; }
        public int? RoomTypeRemarkProperty { get; set; }
        public int? PromotionCategoryId { get; set; }
        public int? PromotionProperty { get; set; }
        public int SupID { get; set; }
        public int BedTypeProperty { get; set; }
        public int AddPriceType { get; set; }
        public decimal AddPriceValue { get; set; }
        public int? IsPush { get; set; }
        public string CreateMsg { get; set; }
        public bool? IsCancel { get; set; }
        public DateTime? CreateTime { get; set; }
        public bool? IsInstantConfirm { get; set; }

    }
}

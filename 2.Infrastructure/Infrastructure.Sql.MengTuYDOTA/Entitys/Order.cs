
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("Order")]
    public class Order
    {
        [Key]
        public long Id { get; set; }
        public MTOTA.Entity.Enum.PlatForm PlateForm { get; set; }
        public string Guid { get; set; }
        public string MTOrderCode { get; set; }
        public string PlatOrderCode { get; set; }
        public int OrderStatus { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime AddTime { get; set; }

        public string HotelName { get; set; }
        public string RoomName { get; set; }

        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public decimal? PaymentPrice { get; set; }
        public int RoomNum { get; set; }
        public string MTHotelCode { get; set; }
        public string BookingInformation { get; set; }

    }
}

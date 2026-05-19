using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("OTARoomInfoPre")]
    public class OTARoomInfoPre
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Map to DB column name "MtHotelCode" to match actual schema
        [Column("MtHotelCode")]
        public string MTHotelCode { get; set; }
        public string RoomNameCN { get; set; }
        public string RoomNameEN { get; set; }
        public byte Person { get; set; }
        public int? RoomQuantity { get; set; }
        public short? FloorMin { get; set; }
        public short? FloorMax { get; set; }
        public short? AreaMin { get; set; }
        public short? AreaMax { get; set; }
        public byte? Smoke { get; set; }
        public DateTime CreateTime { get; set; }
        public int? Status { get; set; }
        public string Author { get; set; }
        public int? Source { get; set; }
        public int? SourceType { get; set; }
        public string DefaultWord { get; set; }
        public string BedInfo { get; set; }
        public DateTime? UpdateTime { get; set; }
        public bool IsCleanedValid { get; set; }

        public static implicit operator OTARoomInfoPre(OTARoomInfo source)
        {
            return new OTARoomInfoPre
            {
                Id = source.Id,
                MTHotelCode = source.MTHotelCode,
                RoomNameCN = source.RoomNameCN,
                RoomNameEN = source.RoomNameEN,
                Person = source.Person,
                RoomQuantity = source.RoomQuantity,
                FloorMin = source.FloorMin,
                FloorMax = source.FloorMax,
                AreaMin = source.AreaMin,
                AreaMax = source.AreaMax,
                Smoke = source.Smoke,
                CreateTime = source.CreateTime,
                Status = source.Status,
                Author = source.Author,
                Source = source.Source,
                SourceType = source.SourceType,
                DefaultWord = source.DefaultWord,
                BedInfo = source.BedInfo,
                //UpdateTime = source.
            };
        }
    }

}

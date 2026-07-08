using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("OTARoomInfoExtensionLog")]
    public class OTARoomInfoExtensionLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int OTARoomInfoExtensionId { get; set; }

        [MaxLength(50)]
        public string? MTHotelCode { get; set; }

        public OTARoomInfoExtensionLogType ChangeType { get; set; }

        [MaxLength(100)]
        public string? InternalView { get; set; }

        [MaxLength(100)]
        public string? ExternalView { get; set; }

        [MaxLength(255)]
        public string? InternalBedInfo { get; set; }

        [MaxLength(255)]
        public string? ExternalBedInfo { get; set; }

        [MaxLength(100)]
        public string? InternalArea { get; set; }

        [MaxLength(100)]
        public string? ExternalArea { get; set; }

        [MaxLength(255)]
        public string? Source { get; set; }

        public DateTime CreateTime { get; set; }

        public OTARoomInfoExtension? OTARoomInfoExtension { get; set; }
    }
}

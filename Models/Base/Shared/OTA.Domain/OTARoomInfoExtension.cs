using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("OTARoomInfoExtension")]
    public class OTARoomInfoExtension
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? MTHotelCode { get; set; }

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

        public OTARoomInfo? OTARoomInfo { get; set; }

        public ICollection<OTARoomInfoExtensionLog> Logs { get; set; } = new List<OTARoomInfoExtensionLog>();
    }
}

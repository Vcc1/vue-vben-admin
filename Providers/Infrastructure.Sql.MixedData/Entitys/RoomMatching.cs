using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using OTADomain.Matchings;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MixedData.Entitys
{
    [Table("RoomMatching")]
    public class RoomMatching
    {
        public int Id { get; set; }

        public int? MRoomId { get; set; }
        public int CRoomId { get; set; }

        public RoomMatchStatus RoomMatchingStatus { get; set; }

        public int Laval { get; set; }



        [NotMapped]
        public string MTHoteCode { get; set; }
        public int SupId { get; set; }

        [NotMapped]
        public SupRoomInfoView CRoom { get; set; }
        public int RoomMatchingScore { get; set; }
        public OTARoomInfo MRoom { get; set; }
    }

}

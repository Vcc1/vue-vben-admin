using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructures.Sql.MengTuAPI.Entitys
{
    [Table("SupCountryInfo")]
    public class SupCountryInfo
    {
        [Key]
        public int Id { get; set; }
        public string SCountryCode { get; set; }
        public int MTCountryId { get; set; }
        public string SupFlag { get; set; }
    }
}

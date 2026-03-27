using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("AgodaCountry")]
    public class AgodaCountry
    {
        [Key]
        public int ID { get; set; }

        public string CountryCode { get; set; }

        public string CountryName { get; set; }
    }
}

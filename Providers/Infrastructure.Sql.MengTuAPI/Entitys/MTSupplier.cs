using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructures.Sql.MengTuAPI.Entitys
{
    [Table("MT_Supplier")]
    public class MTSupplier
    {
        [Key]
        public int Id { get; set; }

        public string Flag { get; set; }
        public string Name { get; set; }
    }
}

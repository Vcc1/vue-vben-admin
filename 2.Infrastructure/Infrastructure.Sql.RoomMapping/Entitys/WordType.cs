using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("WordType")]
    public class WordType
    {
        [Key]
        public int Id { get; set; }
        public string WorldType { get; set; }
        public string IsMustNeed { get; set; }
        public int? Level { get; set; }


    }


    [Table("JH_Contrast_WordType")]
    public class ContrastWordType
    {
        [Key]
        public int Id { get; set; }
        public string WorldType { get; set; }
        public string IsMustNeed { get; set; }


    }
}

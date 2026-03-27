using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("JH_NoSymbolAggressive_WordType")]
    public class NoSymbolAggressiveWordType
    {
        [Key]
        public int Id { get; set; }
        public string WorldType { get; set; }
        public string IsMustNeed { get; set; }

    }
}
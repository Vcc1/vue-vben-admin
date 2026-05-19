using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    /// <summary>
    /// 国籍
    /// </summary>
    /// 
    [Table("MT_Country")]
    public class MTCountry
    {
        /// <summary>
        /// 编号
        /// </summary>
        /// 
        [Column("ID")]
        [Key]
        public int ID { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        /// 
        [Column("Name")]
        public string Name { get; set; }
        /// <summary>
        /// 英文名称
        /// </summary>
        /// 
        [Column("EngName")]
        public string EngName { get; set; }
        /// <summary>
        /// 编码
        /// </summary>
        /// 
        [Column("SimpleName")]
        public string SimpleName { get; set; }

        [Column("PYM")]
        public string PYM { get; set; }

        [Column("JM")]
        public string JM { get; set; }

    }
}

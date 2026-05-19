using OTADomain.Matchings;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("JH_NoSymbolAggressive_WordList")]
    public class NoSymbolAggressiveWordList
    {
        [Key]
        public int Id { get; set; }
        public string WordTitle { get; set; }
        [Column("WordName")]
        public string WordCN { get; set; }
        [Column("WordName_En")]
        public string WordEN { get; set; }
        public string SynonymsWordList { get; set; }
        public string AnotherNameWordList { get; set; }
        public int WordType_Id { get; set; }
        public int? State { get; set; }
        public string? IsMustNeed { get; set; }
        public EffectType? EffectType { get; set; }
        public int? Level { get; set; }
    }
    //public enum EffectType : byte
    //{
    //    Default = 0,   // 默认 双向限定
    //    Positive = 1,  // 增益（正向影响） 允许单独出现在右侧 在左侧必须有对应的词
    //    Negative = 2,  // 减益（负向影响） 允许单独出现在左侧 在右侧必须有对应的词
    //}
}
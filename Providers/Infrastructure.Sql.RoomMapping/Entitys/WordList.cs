using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OTADomain.Matchings;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("WordList")]
    public class WordList : IHasEntityConfiguration<WordList>
    {
        [Key]
        public int Id { get; set; }
        public int WordType_Id { get; set; }
        public string WordTitle { get; set; }
        public string WordsJson { get; set; }
        public int? State { get; set; }
        public string IsMustNeed { get; set; }
        public EffectType? EffectType { get; set; }
        public int? Level { get; set; }
        public bool? IsExcludedInOtaRoom { get; set; }
        public string SimilarWordIds { get; set; }
        public string MutuallyExclusiveWordIds { get; set; }

        public String CreateBy { get; set; }
        public DateTime? CreateTime { get; set; }
        public String LastUpdateBy { get; set; }
        public DateTime? LastUpdateTime { get; set; }


        public string Words { get; set; }
        [Column("WordName")]
        public string WordCN { get; set; }
        [Column("WordName_En")]
        public string WordEN { get; set; }
        public string SynonymsWordList { get; set; }
        public string AnotherNameWordList { get; set; }

        public static void Configure(EntityTypeBuilder<WordList> b)
        {
            b.ToTable(tb => tb.HasTrigger("trg_WordList_Update"));
        }
    }
    /// <summary>
    /// 一组用来对比的配置
    /// </summary>
    [Table("JH_Contrast_WordList")]
    public class ContrastWordList
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
}

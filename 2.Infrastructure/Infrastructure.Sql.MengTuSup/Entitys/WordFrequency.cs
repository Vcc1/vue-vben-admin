using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("WordFrequency")]
    public class WordFrequency
    {
        public int Id { get; set; }
        public string Name { get; set; }   // 判断依据
        public int Quantity { get; set; }  // 数量
        public int WordId { get; set; }    // 插入时必须带上的字段
    }
}

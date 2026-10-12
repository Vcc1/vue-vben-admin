using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Sql.MixedData.Entitys;

public class WordRepeatStat
{
    [Key] // ✅ 添加主键标记
    public int Id { get; set; } // 可以自增，也可以手动填
    public int RoomId { get; set; }
    public string RoomNameOriginal { get; set; }
    public string WordText { get; set; }
    public int RepeatCount { get; set; }
    public int WordAttrId { get; set; }
    public int WordAttrTypeId { get; set; }
}


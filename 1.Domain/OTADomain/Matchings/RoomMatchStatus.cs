namespace OTADomain.Matchings
{
    public enum RoomMatchStatus : byte
    {
        NotMatch = 0,        // 不匹配
        Match2 = 4,
        Match = 1,           // 必须词 + 未知词一致
        FullMatch = 2,       // 全量匹配
        TextMatch = 3        // 全量匹配（包括顺序）
    }

}

namespace RoomMatch.Core.RoomAnalysis
{
    public interface IRoomUnknownCharRepository
    {
        void Report(System.Text.Rune rune);

        void Report(char ch);
    }
}

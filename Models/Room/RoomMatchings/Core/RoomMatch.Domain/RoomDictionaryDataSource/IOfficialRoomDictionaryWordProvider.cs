namespace RoomMatch.Core.RoomDictionaryDataSource
{
    public interface IOfficialRoomDictionaryWordProvider
    {
        IReadOnlyList<RoomDictionaryWordDefinition> Load();
    }
}

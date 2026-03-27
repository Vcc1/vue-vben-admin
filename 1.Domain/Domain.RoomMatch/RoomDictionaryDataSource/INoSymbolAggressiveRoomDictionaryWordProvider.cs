namespace RoomMatch.Core.RoomDictionaryDataSource
{
    public interface INoSymbolAggressiveRoomDictionaryWordProvider
    {
        IReadOnlyList<RoomDictionaryWordDefinition> Load();
    }
}

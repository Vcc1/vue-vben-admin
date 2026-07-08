namespace RoomMatch.Core.RoomDictionaryDataSource
{
    public interface IContrastOfficialRoomDictionaryWordProvider
    {
        IReadOnlyList<RoomDictionaryWordDefinition> Load();
    }
}

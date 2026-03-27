using OTADomain.Matchings;

namespace RoomMatch.Core.RoomDictionaryDataSource
{
    public class RoomDictionaryWordDefinition
    {
        public int Id { get; init; }
        public int WordTypeId { get; init; }
        public string WordTypeName { get; init; } = string.Empty;
        public int? Level { get; init; }
        public string WordTitle { get; init; } = string.Empty;
        public string WordCN { get; init; } = string.Empty;
        public string WordEN { get; init; } = string.Empty;
        public string IsMustNeed { get; init; } = string.Empty;
        public EffectType? EffectType { get; init; }
        public bool IsExcludedInOtaRoom { get; init; }
        public IReadOnlyList<string> WordsJsonWordList { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> SynonymsWordList { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> AnotherNameWordList { get; init; } = Array.Empty<string>();
    }
}

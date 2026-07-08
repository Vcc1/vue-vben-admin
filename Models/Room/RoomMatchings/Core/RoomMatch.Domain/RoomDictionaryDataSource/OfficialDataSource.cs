using Framework.Core.IOC;
using NReco.Text;
using OTADomain.Matchings;
using RoomMatch.Core.RoomDictionary;

namespace RoomMatch.Core.RoomDictionaryDataSource
{
    /// <summary>
    /// 普通据源
    /// </summary>
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class OfficialDataSource
    {
        private readonly IOfficialRoomDictionaryWordProvider _wordProvider;

        public string MD5Key { get; set; }
        List<WordAttribute> wordAttributes = new List<WordAttribute>();
        public AhoCorasickDoubleArrayTrie<WordAttribute> _ACDAT { get; private set; } = new();

        public OfficialDataSource(IOfficialRoomDictionaryWordProvider wordProvider)
        {
            _wordProvider = wordProvider;
            Load();
        }

        public void Load()
        {
            var words = _wordProvider.Load();
            wordAttributes = words.SelectMany(e =>
            {
                var list = new List<WordAttribute>();
                if (string.IsNullOrWhiteSpace(e.WordTitle))
                {
                    return list;
                }
                if (e.WordTitle == "空格")
                {
                    list.Add(new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = " ",
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default,
                        IsExcludedInOtaRoom = e.IsExcludedInOtaRoom,
                    });
                }

                var synonymsWordList = e.WordsJsonWordList.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
                if (synonymsWordList.Count > 0)
                {
                    list.AddRange(synonymsWordList.Select(s => new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = s.ToLower().Trim(),
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default,
                        IsExcludedInOtaRoom = e.IsExcludedInOtaRoom,
                    }));
                }
                return list;
            }).Where(e => !string.IsNullOrWhiteSpace(e.WordText) && e.WordName != "空格").OrderBy(e => e.WordText.ToLower()).OrderBy(e => e.Id).OrderBy(e => e.WordTypeId).ToList();
            ACDATBuild();
        }

        private void ACDATBuild()
        {
            _ACDAT.Build(wordAttributes.Select(e => new KeyValuePair<string, WordAttribute>(e.WordText, e)), false);
        }
    }
}

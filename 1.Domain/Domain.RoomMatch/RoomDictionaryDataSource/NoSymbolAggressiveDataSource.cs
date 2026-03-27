using Framework.Core.IOC;
using NReco.Text;
using OTADomain.Matchings;
using RoomMatch.Core.RoomDictionary;

namespace RoomMatch.Core.RoomDictionaryDataSource
{
    /// <summary>
    /// 无符号激进据源
    /// </summary>
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class NoSymbolAggressiveDataSource
    {
        private readonly INoSymbolAggressiveRoomDictionaryWordProvider _wordProvider;

        public string MD5Key { get; set; }
        List<WordAttribute> wordAttributes = new List<WordAttribute>();
        public AhoCorasickDoubleArrayTrie<WordAttribute> _ACDAT = new();

        public NoSymbolAggressiveDataSource(INoSymbolAggressiveRoomDictionaryWordProvider wordProvider)
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
                        EffectType = e.EffectType ?? EffectType.Default
                    });
                }
                if (!string.IsNullOrWhiteSpace(e.WordCN))
                {
                    list.Add(new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = e.WordCN.Trim(),
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default
                    });
                }
                if (!string.IsNullOrWhiteSpace(e.WordEN))
                {
                    list.Add(new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = e.WordEN.Trim(),
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default
                    });
                }
                var synonymsWordList = e.SynonymsWordList.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
                if (synonymsWordList.Count > 0)
                {
                    list.AddRange(synonymsWordList.Select(s => new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = s.Trim(),
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default
                    }));
                }
                var anotherWordList = e.AnotherNameWordList.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
                if (anotherWordList.Count > 0)
                {
                    list.AddRange(anotherWordList.Select(s => new WordAttribute
                    {
                        Id = e.Id,
                        WordTypeId = e.WordTypeId,
                        WordTypeName = e.WordTypeName,
                        Level = e.Level,
                        WordName = e.WordTitle.Trim(),
                        WordText = s.Trim(),
                        IsMustNeed = e.IsMustNeed == "Y",
                        EffectType = e.EffectType ?? EffectType.Default
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

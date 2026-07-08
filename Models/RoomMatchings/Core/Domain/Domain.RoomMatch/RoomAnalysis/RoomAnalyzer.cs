using Framework.Core.IOC;
using RoomMatch.Core.RoomDictionary;
using RoomMatch.Core.RoomDictionaryDataSource;
using System.Runtime.InteropServices;
using ToolGood.Words;

namespace RoomMatch.Core.RoomAnalysis
{
    [Service]
    public class RoomAnalyzer : IRoomAnalyzer
    {
        readonly IRoomUnknownCharRepository _roomUnknownCharRepository;
        private IRoomWordDictionary _roomDictionary { get; set; }
        private OfficialDataSource officialDataSource { get; set; }
        private ContrastOfficialDataSource contrastOfficialDataSource { get; set; }

        public RoomAnalyzer(IRoomWordDictionary roomDictionary,
            OfficialDataSource noSymbolOfficialDataSource,
            ContrastOfficialDataSource contrastNoSymbolOfficialDataSource,
            IRoomUnknownCharRepository roomUnknownCharRepository)
        {
            _roomDictionary = roomDictionary;
            officialDataSource = noSymbolOfficialDataSource;
            contrastOfficialDataSource = contrastNoSymbolOfficialDataSource;
            _roomUnknownCharRepository = roomUnknownCharRepository;
        }

        RoomAnalysisSetting DefaultSetting { get; set; } = new RoomAnalysisSetting();

        public RoomContext Analysis(RoomContext roomContext)
        {
            try
            {
                roomContext.Setting = roomContext.Setting ?? DefaultSetting;
                var roomName = roomContext.RoomNameOriginal;

                #region 格式乱码修复

                #endregion

                #region 标准化 
                try
                {
                    roomName = RemoveDiacriticsFast(roomName);
                    roomName = WordsHelper.ToSimplifiedChinese(roomName);

                    if (ContainsNonChineseEnglishSymbol(roomName))
                    {
                        roomContext.ErrorMsg = "含其他国家字符";
                        return roomContext;
                    }
                    roomName = NormalizeText(roomName, roomContext);
                }
                catch (Exception)
                {
                }
                roomContext.RoomNameCleaned = roomName;
                #endregion

                #region 过滤
                try
                {
                    if (ContainsNonChineseEnglishSymbol(roomName))
                    {
                        return roomContext;
                    }
                }
                catch (Exception)
                {
                }

                #endregion

                #region 分词
                try
                {
                    if (roomContext.Setting.WordDictSource == WordDictSource.Official)
                    {
                        _roomDictionary.GetWordList(roomContext, officialDataSource._ACDAT);
                    }
                    else if (roomContext.Setting.WordDictSource == WordDictSource.Contrast_Official)
                    {
                        _roomDictionary.GetWordList(roomContext, contrastOfficialDataSource._ACDAT);
                    }
                    else
                    {
                        throw new Exception("不支持的AC分词数据源");
                    }
                }
                catch (Exception)
                {
                }
                if (roomContext.RoomNameWords.Any(e => e.WordAttr.Id == -1))
                {
                }
                #endregion

                #region 分词结果辅助查询处理
                FilterRepeatedGroups(roomContext.RoomNameWords);
                ApplyMutualExclusion(roomContext.RoomNameWords, excludeDics);
                #endregion
            }
            catch (Exception)
            {
                throw;
            }

            return roomContext;
        }

        #region 分组去重
        private static bool DictionaryEqual<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
                if (!b.TryGetValue(kv.Key, out var val) || !EqualityComparer<TValue>.Default.Equals(val, kv.Value))
                    return false;
            return true;
        }
        public static void FilterRepeatedGroups(List<RoomWord> items)
        {
            if (items == null || items.Count == 0)
                return;

            Span<RoomWord> spanItems = CollectionsMarshal.AsSpan(items);

            List<int> idList = new(items.Count);
            foreach (ref readonly var item in spanItems)
            {
                int id = item.WordAttr.Id;
                if (id == -1)
                    return;
                if (id >= 0)
                    idList.Add(id);
            }

            if (idList.Count == 0)
                return;

            int totalCount = idList.Count;

            var idGroup = idList.GroupBy(id => id)
                                .Select(g => new { Id = g.Key, Count = g.Count() })
                                .ToList();

            int maxCount = idGroup.Max(x => x.Count);
            int groupSize = idGroup.Count;

            if (maxCount < 2 || maxCount * groupSize != totalCount)
                return;

            Dictionary<int, int> firstGroup = new();
            for (int i = 0; i < groupSize; i++)
            {
                int id = idList[i];
                if (!firstGroup.TryAdd(id, 1))
                    firstGroup[id]++;
            }

            for (int i = 1; i < maxCount; i++)
            {
                int offset = i * groupSize;
                var tempDict = new Dictionary<int, int>(groupSize);
                for (int j = 0; j < groupSize; j++)
                {
                    int id = idList[offset + j];
                    if (!tempDict.TryAdd(id, 1))
                        tempDict[id]++;
                }

                if (!DictionaryEqual(firstGroup, tempDict))
                    return;
            }

            int excluded = 0;
            foreach (ref var item in spanItems)
            {
                if (item.WordAttr.Id >= 0)
                {
                    if (++excluded > groupSize)
                        item.IsExclude = true;
                }
            }
        }
        #endregion

        #region 失效词
        Dictionary<long, List<int>> excludeDics = new Dictionary<long, List<int>>
        {
            [2790] = new List<int> { 1, 2, 3, 4 }
        };
        public static void ApplyMutualExclusion(List<RoomWord> items, Dictionary<long, List<int>> excludeDics)
        {
            if (items == null || items.Count == 0 || excludeDics == null || excludeDics.Count == 0)
                return;

            var typeIdMap = excludeDics.ToDictionary(kv => kv.Key, kv => kv.Value.ToHashSet());

            var groupedById = new Dictionary<long, List<RoomWord>>();
            var groupedByType = new Dictionary<long, List<RoomWord>>();

            foreach (var item in items)
            {
                var id = item.WordAttr.Id;
                var typeId = item.WordAttr.WordTypeId;

                if (excludeDics.ContainsKey(id))
                {
                    if (!groupedById.TryGetValue(id, out var idList))
                        groupedById[id] = idList = new List<RoomWord>();
                    idList.Add(item);
                }

                foreach (var kv in typeIdMap)
                {
                    var targetId = kv.Key;
                    var typeSet = kv.Value;

                    if (id != targetId && typeSet.Contains(typeId))
                    {
                        if (!groupedByType.TryGetValue(targetId, out var typeList))
                            groupedByType[targetId] = typeList = new List<RoomWord>();
                        typeList.Add(item);
                    }
                }
            }

            foreach (var kv in excludeDics.Keys)
            {
                groupedById.TryGetValue(kv, out var idWords);
                groupedByType.TryGetValue(kv, out var typeWords);

                if (idWords == null || idWords.Count == 0)
                    continue;

                if (typeWords != null && typeWords.Count > 0)
                {
                    foreach (var word in idWords)
                        word.IsExclude = true;
                }
                else if (idWords.Count > 1)
                {
                    for (int i = 1; i < idWords.Count; i++)
                        idWords[i].IsExclude = true;
                }
            }
        }
        #endregion

        public RoomContext<T> Analysis<T>(RoomContext<T> room)
        {
            Analysis((RoomContext)room);
            return room;
        }

        private void Print(RoomContext roomContext)
        {
            Console.WriteLine($"房型源名:{roomContext.RoomNameOriginal}");
            Console.WriteLine($"房型清洁:{roomContext.RoomNameCleaned}");
            Console.WriteLine($"房型格式化:{roomContext.RoomNameCleaned}");
            foreach (var word in roomContext.RoomNameWords)
            {
                Console.WriteLine($"{word.WordAttr.WordText} ----- {word.WordAttr.WordName}({word.WordAttr.WordTypeName})({word.Begin}-{word.End})");
            }
        }

        public static bool ContainsNonChineseEnglishSymbol(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            var nonChat = new List<char>();
            ReadOnlySpan<char> span = input.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];

                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    continue;

                if (c >= 0x4E00 && c <= 0x9FFF)
                    continue;

                if (IsCommonSymbol(c))
                    continue;

                if (_diacriticMap.ContainsKey(c))
                {
                    continue;
                }

                if (c < 32 || (c >= 127 && c <= 159))
                {
                    continue;
                }

                if (IsOtherLanguageCharacter(c))
                {
                    return true;
                }
                else
                {
                    nonChat.Add(c);
                }
            }
            if (nonChat.Count > 0)
            {
            }
            return false;
        }

        #region 音符处理
        private static readonly Dictionary<char, char> _diacriticMap = new Dictionary<char, char>
        {
            ['á'] = 'a',
            ['à'] = 'a',
            ['ä'] = 'a',
            ['â'] = 'a',
            ['å'] = 'a',
            ['é'] = 'e',
            ['è'] = 'e',
            ['ê'] = 'e',
            ['ë'] = 'e',
            ['í'] = 'i',
            ['ì'] = 'i',
            ['ï'] = 'i',
            ['î'] = 'i',
            ['ó'] = 'o',
            ['ò'] = 'o',
            ['ö'] = 'o',
            ['ô'] = 'o',
            ['ú'] = 'u',
            ['ù'] = 'u',
            ['ü'] = 'u',
            ['û'] = 'u',
            ['ñ'] = 'n',
            ['ç'] = 'c',
            ['Á'] = 'A',
            ['À'] = 'A',
            ['Ä'] = 'A',
            ['Â'] = 'A',
            ['Å'] = 'A',
            ['É'] = 'E',
            ['È'] = 'E',
            ['Ê'] = 'E',
            ['Ë'] = 'E',
            ['Í'] = 'I',
            ['Ì'] = 'I',
            ['Ï'] = 'I',
            ['Î'] = 'I',
            ['Ó'] = 'O',
            ['Ò'] = 'O',
            ['Ö'] = 'O',
            ['Ô'] = 'O',
            ['Ú'] = 'U',
            ['Ù'] = 'U',
            ['Ü'] = 'U',
            ['Û'] = 'U',
            ['Ñ'] = 'N',
            ['Ç'] = 'C'
        };

        public static string RemoveDiacriticsFast(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            Span<char> buffer = input.Length <= 256 ? stackalloc char[256] : new char[input.Length];
            int j = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c < 128)
                {
                    buffer[j++] = c;
                    continue;
                }

                if (_diacriticMap.TryGetValue(c, out var mapped))
                {
                    buffer[j++] = mapped;
                }
                else
                {
                    buffer[j++] = c;
                }
            }

            return new string(buffer.Slice(0, j));
        }
        #endregion

        static HashSet<char> specialChars = new HashSet<char>
        {
            ' ',' ', '.', ',', '!', '?', '@', '#', '$', '%', '&', '*',
            '(', ')', '-', '_', '+', '=', '/', '\\', '|',
            '[', ']', '{', '}', ':', ';', '"', '\'', '<', '>',
            '~', '㎡',
            '★', '☆', '＜', '＞', '～', '■', '²', '×', '→', '＆', '《', '》', '◆', '◆', '◇', '「', '」', '｜', '●', '《', '》',
            '（', '）', '【', '】', '｛', '｝', '\t','\n', '\r', '^', '`', '–', '□', '、', '。', '！', '，', '？'
        };

        static Dictionary<char, char> charMap = new Dictionary<char, char>()
        {
             {'_','-' },
             {'-','-' },
             {'—','-' },
             {'–','-' },
             {'“','"' },
             {'”','"' },
             {'‘','\'' },
             {'’','\'' },
             {'★',',' },
             {'☆',',' },
             {'■',',' },
             {'◆',',' },
             {'◇',',' },
             {'●',',' },
             {'\t',',' },
             {'（','(' },
             {'）',')' },
             {'【','[' },
             {'】',']' },
             {'｛','[' },
             {'｝',']' },
             {'?',' ' },
             {'「','[' },
             {'」',']' },
             {'＜','<' },
             {'＞','>' },
             {'《','<' },
             {'》','>' },
             {'～','~' },
             {'｜','|' },
             {'×','*' },
             {'→',',' },
             {'＆','&' },
        };

        static Dictionary<char, char> charMapToSp = new Dictionary<char, char>()
        {
             {'_',' ' },
             {'-',' ' },
             {'—',' ' },
             {'“',' ' },
             {'”',' ' },
             {'‘',' ' },
             {'’',' ' },
             {'★',' ' },
             {'☆',' ' },
             {'■',' ' },
             {'◆',' ' },
             {'◇',' ' },
             {'●',' ' },
             {'\t',' ' },
             {'（',' ' },
             {'）',' ' },
             {'【',' ' },
             {'】',' ' },
             {'｛',' ' },
             {'｝',' ' },
             {'「',' ' },
             {'」',' ' },
             {'＜',' ' },
             {'＞',' ' },
             {'《',' ' },
             {'》',' ' },
             {'～',' ' },
             {'｜',' ' },
             {'×',' ' },
             {'→',' ' },
             {'＆',' ' },
             { '\n', ' ' },
             { '\r', ' ' },
             { '^',  ' ' },
             { '`',  ' ' },
             { '–',  ' ' },
             { '□',  ' ' },
             { '、', ' ' },
             { '。', ' ' },
             { '！', ' ' },
             { '，', ' ' },
             { '？', ' ' },
             { ' ', ' ' },
             { '!', ' ' },
             { '"', ' ' },
             { '#', ' ' },
             { '$', ' ' },
             { '%', '%' },
             { '&', ' ' },
             { '\'', ' ' },
             { '(', ' ' },
             { ')', ' ' },
             { '*', ' ' },
             { '+', ' ' },
             { ',', ' ' },
             { '.', ' ' },
             { '/', '/' },
             { ':', ' ' },
             { ';', ' ' },
             { '<', ' ' },
             { '=', ' ' },
             { '>', ' ' },
             { '?', ' ' },
             { '@', ' ' },
             { '[', ' ' },
             { '\\', ' ' },
             { ']', ' ' },
             { '{', ' ' },
             { '|', ' ' },
             { '}', ' ' },
             { '~', ' ' },
             { '²', '2' },
             { '㎡', ' ' }
        };

        private static bool IsCommonSymbol(char c)
        {
            return specialChars.Contains(c);
        }

        private static bool IsOtherLanguageCharacter(int code)
        {
            return (code >= 0x3040 && code <= 0x30FF)
                || (code >= 0xAC00 && code <= 0xD7AF)
                || (code >= 0x0400 && code <= 0x04FF)
                || (code >= 0x0600 && code <= 0x06FF)
                || (code >= 0x0370 && code <= 0x03FF)
                || (code >= 0x0E00 && code <= 0x0E7F)
                || (code >= 0x1780 && code <= 0x17FF)
                || (code >= 0x1E00 && code <= 0x1EFF);
        }

        public unsafe string NormalizeText(string input, RoomContext roomContext)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = new string('\0', input.Length);
            var ccharMap = roomContext.Setting.CleanFormatType == CleanFormatType.STRONG_NORMALIZE ? charMapToSp : charMap;

            fixed (char* pIn = input)
            fixed (char* pOut = result)
            {
                int j = 0;
                bool lastWasSpace = false;
                bool lastIsMapped = false;

                for (int i = 0; i < input.Length; i++)
                {
                    char c = pIn[i];
                    bool isMapped = false;

                    if (c == 0x3000 || c == 0x00A0)
                        c = ' ';
                    else if (c >= 0xFF01 && c <= 0xFF5E)
                        c = (char)(c - 0xFEE0);

                    if (c >= 'A' && c <= 'Z')
                        c = (char)(c + 32);

                    if (c < 32 || (c >= 127 && c <= 159))
                    {
                        c = ' ';
                    }

                    if (ccharMap.TryGetValue(c, out var mapped))
                    {
                        c = mapped;
                        isMapped = true;
                    }
                    else
                    {
                        bool isChinese = (c >= '\u4E00' && c <= '\u9FFF');
                        bool isLetter = (c >= 'a' && c <= 'z');
                        bool isDigit = (c >= '0' && c <= '9');
                        bool isSpace = IsCommonSymbol(c);
                        if (!isChinese && !isLetter && !isDigit && !isSpace && c != ' ')
                        {
                            roomContext.IsUnknownChar = true;
                            _roomUnknownCharRepository.Report(c);
                            c = ' ';
                        }
                    }

                    if (c == ' ')
                    {
                        if (lastWasSpace) continue;
                        if (j == 0) continue;

                        char next = (i < input.Length - 1) ? pIn[i + 1] : '\0';
                        bool nextIsMapped = ccharMap.ContainsKey(next);

                        if (lastIsMapped || nextIsMapped)
                            continue;

                        lastWasSpace = true;
                    }
                    else
                    {
                        lastWasSpace = false;
                    }

                    pOut[j++] = c;
                    lastIsMapped = isMapped;
                }

                if (j > 0 && pOut[j - 1] == ' ')
                    j--;

                return new string(pOut, 0, j);
            }
        }

        public static unsafe string NormalizeText2(string input, RoomAnalysisSetting setting)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            string result = new string('\0', input.Length);

            var ccharMap = setting.CleanFormatType == CleanFormatType.STRONG_NORMALIZE ? charMapToSp : charMap;

            fixed (char* pIn = input)
            fixed (char* pOut = result)
            {
                int j = 0;
                bool lastWasSpace = false;
                for (int i = 0; i < input.Length; i++)
                {
                    char c = pIn[i];
                    if (c == 0x3000)
                        c = ' ';
                    else if (c >= 0xFF01 && c <= 0xFF5E)
                        c = (char)(c - 0xFEE0);

                    if (c >= 'A' && c <= 'Z')
                        c = (char)(c + 32);

                    if (ccharMap.ContainsKey(c))
                    {
                        c = ccharMap[c];
                    }
                    else
                    {
                        bool isChinese = (c >= '\u4E00' && c <= '\u9FFF');
                        bool isLetter = ((c >= 'a' && c <= 'z'));
                        bool isDigit = (c >= '0' && c <= '9');
                        bool isSpace = IsCommonSymbol(c);
                        if (!isChinese && !isLetter && !isDigit && !isSpace)
                        {
                            c = ' ';
                        }
                    }

                    if (c == ' ')
                    {
                        if (lastWasSpace) continue;
                        lastWasSpace = true;
                    }
                    else
                    {
                        lastWasSpace = false;
                    }

                    pOut[j++] = c;
                }

                return new string(pOut, 0, j);
            }
        }

        public static unsafe string ReplaceDigitsWithHao(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = new string('\0', input.Length);
            fixed (char* pIn = input)
            fixed (char* pOut = result)
            {
                int j = 0;
                bool inNumber = false;

                for (int i = 0; i < input.Length; i++)
                {
                    char c = pIn[i];

                    if ((c >= '0' && c <= '9') || c == '.')
                    {
                        if (!inNumber)
                        {
                            pOut[j++] = '’';
                            inNumber = true;
                        }
                        continue;
                    }
                    else
                    {
                        inNumber = false;
                    }

                    pOut[j++] = c;
                }

                return new string(pOut, 0, j);
            }
        }
    }
}

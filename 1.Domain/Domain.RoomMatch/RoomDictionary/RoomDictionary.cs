using NReco.Text;
using OTADomain.Matchings;
using RoomMatch.Core.RoomAnalysis;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using static NReco.Text.AhoCorasickDoubleArrayTrie<RoomMatch.Core.RoomDictionary.WordAttribute>;

namespace RoomMatch.Core.RoomDictionary
{
    public class RoomDictionary : IRoomWordDictionary
    {
        public List<AhoCorasickDoubleArrayTrie<WordAttribute>.Hit> AC((string Value, string[] Numbers) str, RoomContext roomContext, AhoCorasickDoubleArrayTrie<WordAttribute> _ACDAT)
        {
            if (roomContext.Setting.WordDictSource == WordDictSource.Official)
            {
                return _ACDAT.ParseText(str.Value);
            }
            else if (roomContext.Setting.WordDictSource == WordDictSource.Contrast_Official)
            {
                return _ACDAT.ParseText(str.Value);
            }
            else
            {
                throw new Exception("词典源错误，请检查配置文件！" + roomContext.Setting.WordDictSource);
            }
        }

        public void GetWordList(RoomContext roomContext, AhoCorasickDoubleArrayTrie<WordAttribute> _ACDAT)
        {
            if (string.IsNullOrEmpty(roomContext.RoomNameCleaned))
            {
                return;
            }

            var chits = _ACDAT.ParseText(roomContext.RoomNameCleaned);
            var rhits = ParseWithOriginalPositionFast(_ACDAT, roomContext.RoomNameCleaned);
            chits.AddRange(rhits);
            var fhits = ParseWithSymbolAsSpace_NoRemap(_ACDAT, roomContext.RoomNameCleaned);
            chits.AddRange(fhits);

            if (roomContext.Setting.IsNumberEscape)
            {
                var nhits = ParseWithNumberAsSymbol(_ACDAT, roomContext.RoomNameCleaned);
                chits.AddRange(nhits);
                var nrhits = ParseWithCleanAndNumberSymbol(_ACDAT, roomContext.RoomNameCleaned);
                chits.AddRange(nrhits);
            }

            var set = new HashSet<(int Begin, int End)>();
            var result = new List<Hit>(chits.Count);
            foreach (var hit in chits)
            {
                var key = (hit.Begin, hit.End);
                if (set.Add(key))
                {
                    result.Add(hit);
                }
            }

            var hits = result.ToArray();

            roomContext.IsExcludedInOtaRoom = result.Any(e => e.Value.IsExcludedInOtaRoom);

            Array.Sort(hits, (a, b) =>
            {
                int keyA = (a.Begin << 10) - a.End;
                int keyB = (b.Begin << 10) - b.End;
                return keyA.CompareTo(keyB);
            });

            var strs = roomContext.RoomNameCleaned.AsSpan();
            var stringIndex = 0;
            var hitIndex = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                var currentHit = hits[i];
                if (i == 0 || currentHit.Begin > hits[i - 1].Begin)
                {
                    hitIndex = i;
                }

                if (currentHit.Begin < stringIndex) continue;
                if (currentHit.Begin < stringIndex) continue;
                if (IsLetterIsolated(strs, currentHit)) continue;

                stringIndex = ProcessRoomWord(hits, hitIndex, currentHit, roomContext);
            }

            var stringEnd = roomContext.RoomNameWords.LastOrDefault()?.End ?? 0;
            ProcessUnknownWord(roomContext, stringEnd, roomContext.RoomNameCleaned.Length);
        }

        public static List<Hit> ParseWithOriginalPositionFast(AhoCorasickDoubleArrayTrie<WordAttribute> trie, string originalText)
        {
            var sb = new StringBuilder(originalText.Length);
            var indexMap = ArrayPool<int>.Shared.Rent(originalText.Length);
            int validCount = 0;
            for (int i = 0; i < originalText.Length; i++)
            {
                if (!char.IsWhiteSpace(originalText[i]))
                {
                    indexMap[validCount] = i;
                    sb.Append(originalText[i]);
                    validCount++;
                }
            }
            var cleanedText = sb.ToString();
            var hits = trie.ParseText(cleanedText);
            var remapped = new List<Hit>(hits.Count);
            foreach (var hit in hits)
            {
                int originalBegin = indexMap[hit.Begin];
                int originalEnd = indexMap[hit.End - 1] + 1;
                if (originalText[originalEnd - 1] == ' ')
                {
                    remapped.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(originalBegin, originalEnd - 1, hit.Value, -100));
                }
                else
                {
                    remapped.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(originalBegin, originalEnd, hit.Value, -100));
                }
            }
            ArrayPool<int>.Shared.Return(indexMap);
            return remapped;
        }

        public static unsafe List<Hit> ParseWithCleanAndNumberSymbol(AhoCorasickDoubleArrayTrie<WordAttribute> trie, string originalText)
        {
            var (cleanedOnceText, numbers, indexMap1) = ReplaceNumbersWithSymbolAndMap(originalText);
            (string cleanedText, int[] indexMap, int length) = CleanAgain(cleanedOnceText, indexMap1);
            var hits = trie.ParseText(cleanedText);
            var result = new List<Hit>(hits.Count);
            foreach (var hit in hits)
            {
                int origBegin = indexMap[hit.Begin];
                int origEnd = hit.End < length ? indexMap[hit.End] : originalText.Length;
                if (originalText[origEnd - 1] == ' ')
                {
                    result.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(origBegin, origEnd - 1, hit.Value, -400));
                }
                else
                {
                    result.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(origBegin, origEnd, hit.Value, -400));
                }
            }

            return result;
        }

        static (string cleanedText, int[] indexMap, int length) CleanAgain(string cleanedOnceText, int[] indexMap)
        {
            var sb = new StringBuilder(cleanedOnceText.Length);
            int[] indexMap2 = ArrayPool<int>.Shared.Rent(cleanedOnceText.Length);
            int validCount = 0;
            for (int i = 0; i < cleanedOnceText.Length; i++)
            {
                char c = cleanedOnceText[i];
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    indexMap2[validCount++] = indexMap[i];
                }
            }

            return (sb.ToString(), indexMap2, validCount);
        }

        public static List<Hit> ParseWithSymbolAsSpace_NoRemap(AhoCorasickDoubleArrayTrie<WordAttribute> trie, string originalText)
        {
            var cleanedChars = new char[originalText.Length];

            for (int i = 0; i < originalText.Length; i++)
            {
                char ch = originalText[i];
                cleanedChars[i] = (ch == '-' || char.IsWhiteSpace(ch)) ? ' ' : ch;
            }

            var cleanedText = new string(cleanedChars);
            var hits = trie.ParseText(cleanedText);
            var result = new List<Hit>(hits.Count);
            foreach (var hit in hits)
            {
                result.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(hit.Begin, hit.End, hit.Value, -100));
            }

            return result;
        }

        public static List<Hit> ParseWithNumberAsSymbol(AhoCorasickDoubleArrayTrie<WordAttribute> trie, string originalText)
        {
            var (cleanedText, numbers, indexMap) = ReplaceNumbersWithSymbolAndMap(originalText);
            var hits = trie.ParseText(cleanedText);
            var result = new List<Hit>(hits.Count);
            foreach (var hit in hits)
            {
                int origBegin = indexMap[hit.Begin];
                int origEnd = hit.End < indexMap.Length ? indexMap[hit.End] : originalText.Length;

                if (originalText[origEnd - 1] == ' ')
                {
                    result.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(origBegin, origEnd - 1, hit.Value, -200));
                }
                else
                {
                    result.Add(new AhoCorasickDoubleArrayTrie<WordAttribute>.Hit(origBegin, origEnd, hit.Value, -200));
                }
            }

            return result;
        }

        private static unsafe (string Result, string[] Numbers, int[] IndexMap) ReplaceNumbersWithSymbolAndMap(string input)
        {
            if (string.IsNullOrEmpty(input))
                return (input, Array.Empty<string>(), Array.Empty<int>());

            List<string> numbersList = new();
            List<string> tempNumberList = new();

            string result = new string('\0', input.Length);
            int[] indexMap = new int[input.Length];

            fixed (char* pIn = input)
            fixed (char* pOut = result)
            {
                int j = 0;
                bool inNumber = false;
                int numberStartIndex = 0;

                for (int i = 0; i < input.Length; i++)
                {
                    char c = pIn[i];

                    if (char.IsDigit(c) || c == '.')
                    {
                        if (!inNumber)
                        {
                            inNumber = true;
                            numberStartIndex = i;
                            tempNumberList.Clear();
                            tempNumberList.Add("");
                        }

                        if (c == '.')
                        {
                            tempNumberList.Add("");
                        }
                        else
                        {
                            tempNumberList[^1] += c;
                        }

                        continue;
                    }

                    if (inNumber)
                    {
                        inNumber = false;

                        bool isSimple = tempNumberList.Count == 1 ||
                                        (tempNumberList.Count == 2 && tempNumberList[0] != "" && tempNumberList[1] != "");

                        if (isSimple)
                        {
                            pOut[j] = '，';
                            indexMap[j] = numberStartIndex;
                            j++;
                            numbersList.Add(string.Join(".", tempNumberList));
                        }
                        else
                        {
                            int offset = numberStartIndex;

                            for (int k = 0; k < tempNumberList.Count; k++)
                            {
                                string part = tempNumberList[k];
                                if (part.Length > 0)
                                {
                                    pOut[j] = '，';
                                    indexMap[j] = offset;
                                    j++;
                                    offset += part.Length;
                                    numbersList.Add(part);
                                }

                                if (k < tempNumberList.Count - 1)
                                {
                                    pOut[j] = '.';
                                    indexMap[j] = offset;
                                    j++;
                                    offset++;
                                }
                            }
                        }

                        tempNumberList.Clear();
                    }

                    pOut[j] = c;
                    indexMap[j] = i;
                    j++;
                }

                if (inNumber)
                {
                    bool isSimple = tempNumberList.Count == 1 ||
                                    (tempNumberList.Count == 2 && tempNumberList[0] != "" && tempNumberList[1] != "");

                    if (isSimple)
                    {
                        pOut[j] = '，';
                        indexMap[j] = numberStartIndex;
                        j++;
                        numbersList.Add(string.Join(".", tempNumberList));
                    }
                    else
                    {
                        int offset = numberStartIndex;

                        for (int k = 0; k < tempNumberList.Count; k++)
                        {
                            string part = tempNumberList[k];
                            if (part.Length > 0)
                            {
                                pOut[j] = '，';
                                indexMap[j] = offset;
                                j++;
                                offset += part.Length;
                                numbersList.Add(part);
                            }

                            if (k < tempNumberList.Count - 1)
                            {
                                pOut[j] = '.';
                                indexMap[j] = offset;
                                j++;
                                offset++;
                            }
                        }
                    }
                }

                return (new string(pOut, 0, j), numbersList.ToArray(), indexMap[..j]);
            }
        }

        private static void ProcessUnknownWord(RoomContext roomContext, int begin, int end)
        {
            try
            {
                List<RoomWord> list = roomContext.RoomNameWords;
                if (end == begin)
                {
                    return;
                }
                var nonStr = roomContext.RoomNameCleaned.Substring(begin, end - begin);

                if (nonStr[0] == ' ')
                {
                    list.Add(new RoomWord(WordAttribute.SpaceWordAttribute)
                    {
                        Begin = begin++,
                        End = begin,
                    });
                    if (nonStr.Length == 1)
                    {
                        return;
                    }
                    nonStr = nonStr.Substring(1);
                }

                if (roomContext.Setting.IsUnknownWordSplitBySpace)
                {
                    var words = nonStr.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i] != "")
                        {
                            list.Add(new RoomWord(new WordAttribute
                            {
                                Id = -1,
                                WordTypeId = -1,
                                WordTypeName = "未知",
                                Level = 0,
                                WordName = "未知",
                                WordText = words[i],
                                IsMustNeed = true,
                                EffectType = EffectType.Default
                            })
                            {
                                Begin = begin,
                                End = begin += words[i].Length,
                            });

                            if (i != words.Length - 1)
                            {
                                list.Add(new RoomWord(WordAttribute.SpaceWordAttribute)
                                {
                                    Begin = begin++,
                                    End = begin,
                                });
                            }
                        }
                    }
                }
                else
                {
                    list.Add(new RoomWord(new WordAttribute
                    {
                        Id = -1,
                        WordTypeId = -1,
                        WordTypeName = "未知",
                        Level = 0,
                        WordName = "未知",
                        WordText = nonStr,
                        IsMustNeed = true,
                        EffectType = EffectType.Default
                    })
                    {
                        Begin = begin,
                        End = end,
                    });
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void FillWordParagraphs(Hit[] hits, int hitIndex, Hit previousHit, RoomWord roomWord, RoomContext roomContext)
        {
            try
            {
                roomWord.Hits = roomWord.Hits ?? new List<AhoCorasickDoubleArrayTrie<WordAttribute>.Hit>();
                for (int j = hitIndex; j < hits.Length && hits[j].Begin < previousHit.End; j++)
                {
                    if (hits[j].End > previousHit.End)
                    {
                        continue;
                    }
                    var itemJHits = hits[j];
                    if (itemJHits.Value.WordTypeId == 22)
                    {
                        roomWord.Hits.Add(itemJHits);
                        if (itemJHits.Value.Id == 37624)
                        {
                            try
                            {
                                roomWord.Numbers.Add(decimal.Parse(roomContext.RoomNameCleaned.AsSpan(itemJHits.Begin, itemJHits.End - itemJHits.Begin)));
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLetterIsolated(ReadOnlySpan<char> text, Hit hit)
        {
            int begin = hit.Begin;
            int end = hit.End;

            if (begin < text.Length && IsAsciiLetter(text[begin]))
            {
                if (begin > 0 && IsAsciiLetter(text[begin - 1]))
                    return true;
            }

            if (end > 0 && end - 1 < text.Length && IsAsciiLetter(text[end - 1]))
            {
                if (end < text.Length && IsAsciiLetter(text[end]))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        }

        private bool IsEnglishWord(ReadOnlySpan<char> word)
        {
            var c = word[word.Length - 1];
            if (!(char.IsLetter(c) || c == '\''))
                return false;
            return true;
        }

        public static unsafe (string, string[]) ReplaceNumbersWithSymbol(string input, RoomAnalysisSetting setting)
        {
            if (string.IsNullOrEmpty(input))
            {
                return (input, new string[0]);
            }
            List<string> numbersList = new List<string>();
            List<string> tempNumberList = new List<string>();
            string result = new string('\0', input.Length);
            fixed (char* pIn = input)
            fixed (char* pOut = result)
            {
                int j = 0;
                bool inNumber = false;
                int currentIndex = 0;

                for (int i = 0; i < input.Length; i++)
                {
                    char c = pIn[i];

                    if ((c >= '0' && c <= '9') || c == '.')
                    {
                        if (!inNumber)
                        {
                            inNumber = true;
                            tempNumberList.Add("");
                            currentIndex = tempNumberList.Count - 1;
                        }
                        if (c == '.')
                        {
                            tempNumberList.Add("");
                            currentIndex++;
                        }
                        else
                        {
                            tempNumberList[currentIndex] += c;
                        }
                        continue;
                    }

                    if (inNumber)
                    {
                        inNumber = false;
                        bool isSimpleNumber = tempNumberList.Count == 1 ||
                                              (tempNumberList.Count == 2 && tempNumberList[0] != "" && tempNumberList[1] != "");
                        if (isSimpleNumber)
                        {
                            pOut[j++] = '，';
                            numbersList.Add(string.Join(".", tempNumberList));
                        }
                        else
                        {
                            for (int k = 0; k < tempNumberList.Count; k++)
                            {
                                if (!string.IsNullOrEmpty(tempNumberList[k]))
                                {
                                    pOut[j++] = '，';
                                    numbersList.Add(tempNumberList[k]);
                                }
                                if (k < tempNumberList.Count - 1)
                                {
                                    pOut[j++] = '.';
                                }
                            }
                        }
                        tempNumberList.Clear();
                    }
                    pOut[j++] = c;
                }
                if (inNumber)
                {
                    bool isSimpleNumber = tempNumberList.Count == 1 ||
                                          (tempNumberList.Count == 2 && tempNumberList[0] != "" && tempNumberList[1] != "");

                    if (isSimpleNumber)
                    {
                        pOut[j++] = '\'';
                        numbersList.Add(string.Join(".", tempNumberList));
                    }
                    else
                    {
                        for (int k = 0; k < tempNumberList.Count; k++)
                        {
                            if (!string.IsNullOrEmpty(tempNumberList[k]))
                            {
                                pOut[j++] = '\'';
                                numbersList.Add(tempNumberList[k]);
                            }
                            if (k < tempNumberList.Count - 1)
                            {
                                pOut[j++] = '.';
                            }
                        }
                    }
                }

                return (new string(pOut, 0, j), numbersList.ToArray());
            }
        }

        private int ProcessRoomWord(Hit[] hits, int hitIndex, Hit previousHit, RoomContext roomContext)
        {
            var list = roomContext.RoomNameWords;
            int stringIndex;
            var roomWord = new RoomWord(previousHit.Value)
            {
                Begin = previousHit.Begin,
                End = previousHit.End,
            };
            var stringEnd = list.LastOrDefault()?.End ?? 0;
            if (roomWord.Begin > stringEnd)
            {
                ProcessUnknownWord(roomContext, stringEnd, roomWord.Begin);
            }
            if (roomWord.WordAttr.Id == 37624)
            {
                roomWord.Numbers.Add(decimal.Parse(roomContext.RoomNameCleaned.AsSpan(previousHit.Begin, previousHit.End - previousHit.Begin)));
            }

            list.Add(roomWord);
            stringIndex = previousHit.End;
            FillWordParagraphs(hits, hitIndex, previousHit, roomWord, roomContext);
            return stringIndex;
        }
    }
}

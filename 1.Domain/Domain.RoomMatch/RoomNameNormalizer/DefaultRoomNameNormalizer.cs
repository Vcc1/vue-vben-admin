using Framework.Core.IOC;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomNameNormalizer
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped, FactoryType = typeof(IRoomNameNormalizer))]

    public class DefaultRoomNameNormalizer : IRoomNameNormalizer
    {
        List<(int openId, int closeId)> pairedSymbolIds = new List<(int openId, int closeId)>() { new(1, 2), new(3, 4) };

        List<char> splitMarkers = new List<char>() { ',', '(', ')', '[', ']' };
        public DefaultRoomNameNormalizer()
        {

        }

        public string Normalize(RoomContext roomContext)
        {
            if (!(roomContext?.RoomNameWords?.Count > 0))
                return "";
            return Normalization(roomContext.RoomNameWords, roomContext.RoomNameCleaned);
        }

        public bool FormatComplianceCheck(RoomContext roomContext)
        {
            return CheckSymbolRules(roomContext);
        }

        public bool CheckSymbolRules(RoomContext roomContext)
        {
            if (!(roomContext?.RoomNameWords?.Count > 0))
                return true;

            var words = roomContext.RoomNameWords;
            var stack = new Stack<(RoomWord word, int openId, int closeId)>();

            for (int i = 0; i < words.Count; i++)
            {
                var word = words[i];
                var wordId = word.WordAttr.Id;

                // 查找是否是成对符号
                var pair = pairedSymbolIds.FirstOrDefault(p => p.openId == wordId || p.closeId == wordId);

                if (pair != default && pair.openId == wordId)
                {
                    // 是开始符号
                    stack.Push((word, pair.openId, pair.closeId));
                }
                else if (pair != default && pair.closeId == wordId)
                {
                    // 是结束符号
                    if (stack.Count == 0) return false;

                    var top = stack.Pop();
                    if (top.closeId != wordId) return false;

                    // 获取中间的词
                    var innerWords = words.Where(w =>
                        w.Begin >= top.word.End && w.End <= word.Begin).ToList();

                    // 有被标记为忽略的词
                    bool hasExcluded = innerWords.Any(w => w.WordAttr.IsExcludedInOtaRoom);

                    if (hasExcluded)
                    {
                        foreach (var w in innerWords)
                        {
                            bool isExcluded = w.WordAttr.IsExcludedInOtaRoom;
                            bool isSymbol = pairedSymbolIds.Any(p =>
                                p.openId == w.WordAttr.Id || p.closeId == w.WordAttr.Id);

                            if (!isExcluded && !isSymbol)
                                return false; // 括号中含非法词
                        }
                    }
                }
            }

            return stack.Count == 0; // 所有符号必须配对
        }



        private string Normalization(List<RoomWord> roomNameWords, string roomName)
        {
            var room = roomNameWords.Where(e => !e.WordAttr.IsExcludedInOtaRoom).ToList();
            RemoveAll22RoomWordsBetween37621And37622(room);
            // 步骤 2：去除首尾的空白或“-”字符
            while (room.Count > 0 && IsTrimChar(room[0]))
                room.RemoveAt(0);
            while (room.Count > 0 && IsTrimChar(room[^1]))
                room.RemoveAt(room.Count - 1);
            TrimAroundMarker(room, roomName);

            var nRoomName = string.Join("", room.Select(e => e.SourceText(roomName).ToString()));
            return nRoomName;
        }

        public void TrimAroundMarker(
                                List<RoomWord> room,
                                string roomName)
        {
            for (int index = room.Count - 1; index >= 0; index--)
            {
                var span = room[index].SourceText(roomName);
                if (span.Length == 1 && splitMarkers.Contains(span[0]))
                {

                    bool leftRemovedSpace = false;
                    RoomWord MakeSpaceWord = null;
                    // 向左清除
                    int left = index - 1;
                    while (left >= 0 && IsTrimChar(room[left]))
                    {
                        if (room[left].WordAttr.WordText.Trim() == "")
                        {
                            leftRemovedSpace = true;
                            MakeSpaceWord = room[left];
                        }

                        room.RemoveAt(left);
                        index--; // 因为左边删除，当前标记符位置也向前移动
                        left--;
                    }
                    if (leftRemovedSpace)
                    {
                        room.Insert(index, MakeSpaceWord);
                        index++; // 插入后标记右移一格，保持 markerIndex 指向原标记
                    }

                    bool rightRemovedSpace = false;
                    // 向右清除
                    int right = index + 1;
                    while (right < room.Count && IsTrimChar(room[right]))
                    {
                        if (room[right].WordAttr.WordText.Trim() == "")
                        {
                            rightRemovedSpace = true;
                            MakeSpaceWord = room[right];
                        }
                        room.RemoveAt(right);
                        // 不动 index，right 自动变成下一个（因为列表缩短）
                    }
                    // 右边如果删过空格，就在标记右侧补一个空格
                    if (rightRemovedSpace)
                    {
                        room.Insert(index + 1, MakeSpaceWord);
                    }
                }
            }
        }



        public static void RemoveAll22RoomWordsBetween37621And37622(List<RoomWord> list)
        {
            int i = 0;
            while (i < list.Count)
            {
                if (list[i].WordAttr.Id == 37621)
                {
                    int startIndex = i;
                    int j = i + 1;
                    bool interrupted = false;

                    // 找最近的 37622，如果途中有新的 37621，当前起点作废
                    while (j < list.Count)
                    {
                        int id = list[j].WordAttr.Id;

                        if (id == 37621)
                        {
                            interrupted = true;
                            break;
                        }

                        if (id == 37622)
                            break;

                        j++;
                    }

                    if (!interrupted && j < list.Count && list[j].WordAttr.Id == 37622)
                    {
                        // 判断这段 [i..j] 是否全是 TypeId == 22
                        bool all22 = list.Skip(i).Take(j - i + 1).All(x => x.WordAttr.WordTypeId == 22);
                        if (all22)
                        {
                            list.RemoveRange(i, j - i + 1);
                            continue; // 删除后当前位置不动，重新判断
                        }
                    }
                }

                i++;
            }
        }
        private static bool IsTrimChar(RoomWord roomWord)
        {
            var text = roomWord.WordAttr.WordText.Trim();
            return text == "-" || text == "" || text == "," || text == "+";
        }

    }
}

using NReco.Text;
using RoomMatch.Core.RoomAnalysis;

namespace RoomMatch.Core.RoomDictionary
{
    public interface IRoomWordDictionary
    {
        void GetWordList(RoomContext roomContext, AhoCorasickDoubleArrayTrie<WordAttribute> acdat);
    }
}

using Framework.Core.IOC;
using Microsoft.Extensions.DependencyInjection;
using NReco.Text;
using RoomMatch.Core.RoomAnalysis;

namespace RoomMatch.Core.RoomDictionary
{
    [Service(ServiceLifetime.Singleton)]
    public class RoomDictionarySingleton : IRoomWordDictionary
    {
        public static RoomDictionary CurrentRoomDictionary { get; } = new RoomDictionary();

        public static object Kghit { get; set; }

        public void GetWordList(RoomContext roomContext, AhoCorasickDoubleArrayTrie<WordAttribute> acdat)
        {
            CurrentRoomDictionary.GetWordList(roomContext, acdat);
        }
    }
}

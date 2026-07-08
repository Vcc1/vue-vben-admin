using RoomMatch.Core.RoomAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomAttributed
{
    internal interface IRoomAttributeExtractor
    {
        IRoomAttributeExtractor SetNext(IRoomAttributeExtractor nextHandler);
        void Handle(RoomContext roomData);
    }
    abstract class RoomAttributeExtractor : IRoomAttributeExtractor
    {
        protected IRoomAttributeExtractor _nextHandler;
        public IRoomAttributeExtractor SetNext(IRoomAttributeExtractor nextHandler)
        {
            _nextHandler = nextHandler;
            return nextHandler;
        }
        public virtual void Handle(RoomContext roomData)
        {
            _nextHandler?.Handle(roomData);
        }
    }


    internal class MarketingWordExtractor : RoomAttributeExtractor
    {
        public override void Handle(RoomContext roomData)
        {
            //foreach (var word in MarketingKeywords)
            //{
            //    //if (roomData.RoomName.Contains(word))
            //    //{
            //    //    roomData.MarketingWords.Add(word);
            //    //}
            //}

            base.Handle(roomData);
        }
    }

    internal class DescriptionExtractor : RoomAttributeExtractor
    {
        public override void Handle(RoomContext roomData)
        {
            //roomData.Description = roomData.RoomDescription?.Trim();
            base.Handle(roomData);
        }
    }

    internal class AreaExtractor : RoomAttributeExtractor
    {
        public override void Handle(RoomContext roomData)
        {
            //var match = Regex.Match(roomData.RoomDescription, @"(\d{1,3})\s*㎡");
            //if (match.Success)
            //{
            //    //roomData.Area = int.Parse(match.Groups[1].Value);
            //}

            //Console.WriteLine($"面积提取完成: {roomData.Area} ㎡");
            base.Handle(roomData);
        }
    }

    internal class WindowTypeExtractor : RoomAttributeExtractor
    {
        //private static readonly List<string> WindowTypes = new List<string> { "落地窗", "飘窗", "观景窗", "天窗" };

        public override void Handle(RoomContext roomData)
        {
            //if (roomData.RoomName.Contains("无窗") || roomData.RoomDescription.Contains("无窗"))
            //{
            //    roomData.HasWindow = false;
            //}
            //else
            //{
            //    roomData.HasWindow = true;
            //}

            //foreach (var type in WindowTypes)
            //{
            //    if (roomData.RoomDescription.Contains(type))
            //    {
            //        roomData.WindowType = type;
            //        break;
            //    }
            //}

            //if (string.IsNullOrEmpty(roomData.WindowType))
            //{
            //    roomData.WindowType = "普通窗";
            //}

            //Console.WriteLine($"窗型提取完成: has_window={roomData.HasWindow}, window_type={roomData.WindowType}");
            base.Handle(roomData);
        }
    }


}


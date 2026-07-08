//using Framework.Core.IOC;
//using Infrastructure.Sql.MengTuSup;
//using Infrastructure.Sql.MengTuSup.Entitys;
//using Microsoft.Extensions.DependencyInjection;
//using RoomMatch.Core.RoomAnalysis;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace RoomMatch.Core.Apps
//{
//    [Service]
//    public class EBKRoomCreateApp
//    {
//        private readonly IServiceScopeFactory _scopeFactory;
//        MengTuSupContext _mengTuSupContext;
//        MengTuYDOTAContext _mengTuYDOTAContext;
//        IRoomAnalyzer _roomAnalysis;
//        private readonly IServiceProvider _serviceProvider;

//        public EBKRoomCreateApp(IServiceProvider serviceProvider, IRoomAnalyzer roomAnalysis, MengTuSupContext mengTuSupContext, IServiceScopeFactory scopeFactory, MengTuYDOTAContext mengTuYDOTAContext)
//        {
//            _serviceProvider = serviceProvider;
//            _roomAnalysis = roomAnalysis;
//            _mengTuSupContext = mengTuSupContext;
//            _scopeFactory = scopeFactory;
//            _mengTuYDOTAContext = mengTuYDOTAContext;
//        }


//        public void MatchEBKRoomMappingsByBatch(string batchNo)
//        {
//            var mappings = _mengTuSupContext.EBKRoomMappings.Where(e => e.BatchNo == batchNo).ToList().Select(e => new RoomContext<EBKRoomMapping>(e.RoomName, e)).ToList();
//            var mappingGroupMtHotel = mappings.GroupBy(e => e.SourceData.MtHotelCode);
//            mappings.ForEach(e => _roomAnalysis.Analysis(e));
//            mappings.ForEach(e => e.RoomNameWords = e.RoomNameWords.Where(e => e.WordAttr.WordTypeId != 22).ToList());
//            foreach (var mthotelEBKItem in mappingGroupMtHotel)
//            {
//                var mRoomInfos = _mengTuYDOTAContext.CtripHotelRoomInfos.Where(e => e.MTHotelCode == mthotelEBKItem.Key).ToList()
//                    .Select(e => new RoomContext<CtripHotelRoomInfo>(e.basicRoomNameEn, e)).ToList();


//                #region 处理房型
//                mRoomInfos.ForEach(e => _roomAnalysis.Analysis(e));
//                mRoomInfos.ForEach(e => e.RoomNameWords = e.RoomNameWords.Where(e => e.WordAttr.WordTypeId != 22).ToList());
//                #endregion


//                #region 匹配
//                foreach (var item in mthotelEBKItem)
//                {
//                    var a = RoomMatch(item, mRoomInfos);
//                    if (a != null)
//                    {
//                        item.SourceData.MRoomName = a.RoomNameOriginal;
//                        _mengTuSupContext.Update(item.SourceData);
//                        _mengTuSupContext.SaveChanges();


//                    }
//                }
//                #endregion

//            }
//        }

//        private static RoomContext<CtripHotelRoomInfo> RoomMatch(RoomContext<EBKRoomMapping> item, List<RoomContext<CtripHotelRoomInfo>> mRoomInfos)
//        {
//            var noneWord = item.RoomNameWords.Where(e => e.WordAttr.Id == -1);
//            var requiredWord = item.RoomNameWords.Where(e => e.WordAttr.Id != -1 && e.WordAttr.IsMustNeed);
//            var noRequiredWord = item.RoomNameWords.Where(e => e.WordAttr.Id != -1 && !e.WordAttr.IsMustNeed);

//            foreach (var mRoomInfo in mRoomInfos)
//            {
//                var noneMWord = mRoomInfo.RoomNameWords.Where(e => e.WordAttr.Id == -1);
//                var requiredMWord = mRoomInfo.RoomNameWords.Where(e => e.WordAttr.Id != -1 && e.WordAttr.IsMustNeed);
//                var noRequiredMWord = mRoomInfo.RoomNameWords.Where(e => e.WordAttr.Id != -1 && !e.WordAttr.IsMustNeed);

//                var noneTexts = noneWord.SelectMany(e => e.WordAttr.WordText.Split(' ')).OrderBy(e => e).ToList();
//                var noneMTexts = noneMWord.SelectMany(e => e.WordAttr.WordText.Split(' ')).OrderBy(e => e).ToList();
//                if (noneTexts.Except(noneMTexts).Concat(noneMTexts.Except(noneTexts)).Count() > 0)
//                {
//                    continue;
//                }
//                var requiredMWordId = requiredMWord.Select(e => e.WordAttr.Id).ToList();
//                var requiredWordId = requiredWord.Select(e => e.WordAttr.Id).ToList();

//                if (requiredMWordId.Except(requiredWordId).Concat(requiredWordId.Except(requiredMWordId)).Count() > 0)
//                {
//                    continue;
//                }

//                Console.WriteLine($"房型{item.RoomNameOriginal} 与母房型{mRoomInfo.RoomNameOriginal} 匹配");
//                return mRoomInfo;
//                break;

//            }
//            return null;

//        }


//    }
//}

using Framework.Core.IOC;
using RoomMatch.Core.RoomAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomAttributed
{

    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    internal class RoomAttributeHandle
    {


        static readonly IRoomAttributeExtractor handle = new MarketingWordExtractor()
                   .SetNext(new DescriptionExtractor())
                   .SetNext(new AreaExtractor())
                   .SetNext(new WindowTypeExtractor());

        //internal void Handle(RoomContext roomData)
        //{
        //    #region 语法处理

        //    for (int i = 0; i < roomData.RoomNameTokens.Count; i++)
        //    {
        //        var token = roomData.RoomNameTokens[i];
        //        if (token == "with")
        //        {

        //        }




        //        #region with plan 词处理

        //        /// 先定位到 with plan 
        //        /// 然后向后找 直到找到 结束 词


        //        #endregion


        //        #region with 词处理

        //        /// 先定位到 with
        //        /// 然后向后找 直到找到 结束 词
        //        /// 如果找到床型词 则床型记录为+床

        //        #endregion





        //        #region And 词处理

        //        #endregion



        //        #endregion


        //        roomData.RoomNameTokens

        //    handle.Handle(roomData);
        //    }

        //}


    }
}

using Framework.Core.IOC;
using RoomMatch.Core.RoomAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomNameNormalizer
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton, ServiceType = typeof(DefaultRoomNameNormalizer))]
    public interface IRoomNameNormalizer
    {
        string Normalize(RoomContext roomName);

        /// <summary>
        /// 判断是否能清洗
        /// </summary>
        /// <param name="roomContext"></param>
        /// <returns></returns>
        bool FormatComplianceCheck(RoomContext roomContext);
    }
}

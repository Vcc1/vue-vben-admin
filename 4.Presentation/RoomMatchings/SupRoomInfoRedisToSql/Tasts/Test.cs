/*using EFCore.BulkExtensions;*/
using Framework.Core.IOC;
using Infrastructure.Sql.MengTuYDOTA;
//using Infrastructure.Sql.VBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomNameNormalizer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VBaseFramework.ServiceDefaults;
using VBaseFramework.Core.IOC;
namespace SupRoomInfoRedisToSql
{


    [TestClass]
    public class Test
    {
        private IServiceProvider _provider;
        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void Init()
        {
          
        }

    }
}

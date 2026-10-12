using Framework.Core.IOC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.Auth
{
    [Service(typeof(UserContext), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public interface IUserContext
    {
        /// <summary>
        /// 是否API调用
        /// </summary>
        bool IsApi { get; set; }
        string UserId { get; set; }
        public string Name { get; set; }
        public string[] Roles { get; set; }
    }
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class UserContext : IUserContext
    {

        public string UserId { get; set; }
        public string Name { get; set; }
        public string[] Roles { get; set; }
        /// <summary>
        /// 是否API调用
        /// </summary>
        public bool IsApi { get; set; }
    }
}

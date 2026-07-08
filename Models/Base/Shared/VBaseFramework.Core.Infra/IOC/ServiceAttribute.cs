using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VBaseFramework.Core.IOC;

namespace Framework.Core.IOC
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        /// <summary>
        /// 被注入的类型
        /// </summary>
        public Type ServiceType { get; set; }
        /// <summary>
        /// 生命周期
        /// </summary>
        public ServiceLifetime Lifetime { get; set; }
        /// <summary>
        /// 工厂类型
        /// </summary>
        public Type FactoryType { get; set; }

        /// <summary>
        /// 指定配置文件Key
        /// </summary>
        public string FactoryTypeConfigKey { get; set; }
        public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
        }
        public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
            ServiceType = serviceType;
        }
        public ServiceAttribute(string factoryTypeConfigKey, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
            this.FactoryTypeConfigKey = factoryTypeConfigKey;
        }
    }


    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class ServiceAttribute<T, F> : ServiceAttribute where T : IServiceFactory<F>
    {
        public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
            FactoryType = typeof(T);
        }
        public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
            FactoryType = typeof(T);
        }
    }
}

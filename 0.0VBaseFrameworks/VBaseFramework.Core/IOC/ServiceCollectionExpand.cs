using EntityFrameworkCore.UseRowNumberForPaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VBaseFramework.Core.EF;
using VBaseFramework.Core.EF.Log;
using VBaseFramework.Core.IOC;

namespace Framework.Core.IOC
{
    public static class ServiceCollectionExpand
    {



        private static readonly ConcurrentDictionary<Type, IServiceFactory> FactoryCache = new();

        /// <summary>
        /// 业务服务批量注入
        /// </summary>
        /// <param name="services">DI 容器</param>
        /// <param name="assemblies">需要扫描的程序集</param>
        public static IServiceCollection AddBusiness(this IServiceCollection services, Assembly[] assemblies)
        {
            try
            {

                var serviceTypes = assemblies
                    .SelectMany(x => x.GetTypes())
                    .Where(x => (x.IsClass || x.IsInterface))
                    .Select(e => new
                    {
                        Type = e,
                        Attr = e.GetCustomAttribute<ServiceAttribute>(false)
                    })
                    .Where(e => e.Attr != null)
                    .ToList();


                serviceTypes.ForEach(serviceType =>
                {
                    var lifetime = serviceType.Attr.Lifetime;

                    if (serviceType.Type.IsInterface && (serviceType.Attr.ServiceType == null && serviceType.Attr.FactoryType == null))
                    {
                        // 接口类型，必须指定 ServiceType 和 FactoryType 之一
                    }

                    var type = serviceType.Type;
                    var serviceClassType = serviceType.Attr.ServiceType ?? type;
                    var factoryType = serviceType.Attr.FactoryType;
                    if (factoryType != null && typeof(IServiceFactory).IsAssignableFrom(factoryType))
                    {
                        // 从缓存获取工厂实例，如果没有则创建并缓存
                        var factoryInstance = FactoryCache.GetOrAdd(factoryType, ft =>
                        {
                            var instance = Activator.CreateInstance(ft) as IServiceFactory;
                            if (instance == null)
                            {
                                throw new Exception($"无法创建工厂 {ft.FullName} 的实例");
                            }
                            return instance;
                        });

                        // 通过工厂创建实例
                        Func<IServiceProvider, object> factoryFunc = sp => factoryInstance.CreateInstance(sp);

                        switch (lifetime)
                        {
                            case ServiceLifetime.Transient:
                                services.AddTransient(type, factoryFunc);
                                break;
                            case ServiceLifetime.Singleton:
                                services.AddSingleton(type, factoryFunc);
                                break;
                            case ServiceLifetime.Scoped:
                                services.AddScoped(type, factoryFunc);
                                break;
                        }
                    }
                    else
                    {
                        // 直接注册类型
                        switch (lifetime)
                        {
                            case ServiceLifetime.Transient:
                                services.AddTransient(type, serviceClassType);
                                break;
                            case ServiceLifetime.Singleton:
                                services.AddSingleton(type, serviceClassType);
                                break;
                            case ServiceLifetime.Scoped:
                                services.AddScoped(type, serviceClassType);
                                break;
                        }

                        if (type.IsClass && !type.IsAbstract && serviceType.Attr.ServiceType == null && factoryType == null)
                        {
                            foreach (var implementedInterface in type.GetInterfaces())
                            {
                                switch (lifetime)
                                {
                                    case ServiceLifetime.Transient:
                                        services.AddTransient(implementedInterface, type);
                                        break;
                                    case ServiceLifetime.Singleton:
                                        services.AddSingleton(implementedInterface, type);
                                        break;
                                    case ServiceLifetime.Scoped:
                                        services.AddScoped(implementedInterface, type);
                                        break;
                                }
                            }
                        }
                    }
                });
                AddDbContext(services, assemblies);
            }
            catch (Exception ex)
            {
                throw new Exception("项目启动时业务服务注入异常", ex);
            }

            return services;
        }

        private static void AddDbContext(IServiceCollection services, Assembly[] assemblies)
        {
            var efDBServiceTypes = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => x.IsClass && !x.IsAbstract && typeof(DbContext).IsAssignableFrom(x))
            .SelectMany(type =>
                type.GetCustomAttributes<EFDbServiceAttribute>(false)
                    .Select(attr => new { Type = type, Attr = attr }))
            .ToList();

            //var loggerFactory = new LoggerFactory();
            //loggerFactory.AddProvider(new EFLoggerProvider());


            efDBServiceTypes.ForEach(e =>
            {
                Action<IServiceProvider, DbContextOptionsBuilder> configureDb = (sp, options) =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var loggerFactory = new LoggerFactory();
                    //var loggerFactory = sp.GetRequiredService<LoggerFactory>();
                    var loggerEfFactory = sp.GetRequiredService<EFLoggerProvider>();
                    loggerFactory.AddProvider(loggerEfFactory);
                    var connStr = configuration.GetConnectionString(e.Attr.ConfigKey);

                    if (DetectDbType(connStr) == DbType.SqlServer)
                    {
                        //Trace.WriteLine($"数据库加载 {e.Attr.ConfigKey} 连接字符串:{connStr}");
                        //ob.UseRowNumberForPaging()
                        options.UseSqlServer(connStr, ob => { }).UseLoggerFactory(loggerFactory).EnableSensitiveDataLogging();
                    }
                    else
                    {

                        Console.WriteLine($"不支持的数据库类型: {DetectDbType(connStr)}:{connStr}");
                    }
                };
                var method = FindAddDbContextMethod();
                var genericMethod = method.MakeGenericMethod(e.Type);
                genericMethod.Invoke(null, new object[] { services, configureDb, ServiceLifetime.Scoped, ServiceLifetime.Scoped });
            });
        }




        public static MethodInfo FindAddDbContextMethod()
        {

            return typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AddDbContext" && m.IsGenericMethodDefinition)
                .FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 4) return false;

                    if (parameters[0].ParameterType != typeof(IServiceCollection)) return false;

                    // 第二个参数必须是 Action<IServiceProvider, DbContextOptionsBuilder> 或 Nullable 的
                    if (!parameters[1].ParameterType.IsGenericType) return false;
                    var genericDef = parameters[1].ParameterType.GetGenericTypeDefinition();
                    if (genericDef != typeof(Action<,>)) return false;

                    var args = parameters[1].ParameterType.GetGenericArguments();

                    if (parameters[2].ParameterType != typeof(ServiceLifetime)) return false;
                    if (parameters[3].ParameterType != typeof(ServiceLifetime)) return false;

                    return args[0] == typeof(IServiceProvider) &&
                           args[1] == typeof(DbContextOptionsBuilder);

                });
        }

        public enum DbType
        {
            Unknown,
            SqlServer,
            MySql,
            Sqlite
        }
        public static DbType DetectDbType(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return DbType.Unknown;

            var cs = connectionString.ToLowerInvariant();

            // SQLite
            if ((cs.Contains(".sqlite") || cs.Contains(".db")) && cs.Contains("data source"))
                return DbType.Sqlite;

            // MySQL
            if (cs.Contains("server=") && cs.Contains("port=") &&
                cs.Contains("database=") &&
                (cs.Contains("uid=") || cs.Contains("user id=")))
                return DbType.MySql;

            // SQL Server
            if (cs.Contains("server=") &&
                cs.Contains("database=") &&
                (cs.Contains("uid=") || cs.Contains("user id=")))
                return DbType.SqlServer;

            return DbType.Unknown;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="Assemblys">期望被加载的程序集</param>
        /// <exception cref="Exception"></exception>
        public static void AddBusiness2(this IServiceCollection services, Assembly[] Assemblys)
        {
            try
            {
                var bServices = Assemblys
               .SelectMany(x => x.GetTypes()).Where(x => (x.IsClass || x.IsInterface) && !x.IsAbstract)
               .Select(e => new { Type = e, Attr = e.GetCustomAttribute(typeof(ServiceAttribute), false) as ServiceAttribute })
               .Where(e => e.Attr != null)
               .ToList();
                bServices.ForEach((bServiceType) =>
                {
                    var lifeTime = bServiceType.Attr.Lifetime;
                    var type = bServiceType.Type;
                    var dependentTypes = new List<Type>() { type };
                    foreach (var item in dependentTypes)
                    {
                        switch (lifeTime)
                        {
                            case ServiceLifetime.Transient: services.AddTransient(item, type); break;
                            case ServiceLifetime.Singleton: services.AddSingleton(item, type); break;
                            case ServiceLifetime.Scoped: services.AddScoped(item, type); break;
                        }
                    }

                });
            }
            catch (Exception)
            {
                throw new Exception("项目启动时业务服务注入异常");
            }


        }
    }
}

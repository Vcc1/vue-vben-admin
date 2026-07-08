using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace VBaseFramework.Core.IOC
{


    public static class AttributedOptionsRegistrationExtensions
    {
        public static IServiceCollection AddOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            params Assembly[] assemblies)
        {
            if (assemblies is null || assemblies.Length == 0)
                assemblies = new[] { Assembly.GetExecutingAssembly() };

            foreach (var asm in assemblies)
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract || !type.IsPublic) continue;

                    var attr = type.GetCustomAttribute<ConfigSectionAttribute>();
                    if (attr is null) continue;

                    // 只支持无参 class（普通 POCO）
                    if (type.GetConstructor(Type.EmptyTypes) is null)
                        throw new InvalidOperationException($"{type.FullName} 需要公共无参构造函数。");

                    // 绑定的配置节
                    var section = configuration.GetSection(attr.Section);

                    if (attr.Reloadable)
                    {
                        // Options 模式（支持热更新）
                        var method = typeof(AttributedOptionsRegistrationExtensions)
                            .GetMethod(nameof(ConfigureReloadable), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(type);

                        method.Invoke(null, new object[] { services, section, attr });
                    }
                    else
                    {
                        // 直接强类型单例（不热更新）
                        var method = typeof(AttributedOptionsRegistrationExtensions)
                            .GetMethod(nameof(AddSingletonBound), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(type);

                        method.Invoke(null, new object[] { services, section, attr });
                    }
                }
            }

            return services;
        }

        private static void ConfigureReloadable<T>(
                     IServiceCollection services,
                     IConfiguration section,
                     ConfigSectionAttribute attr) where T : class, new()
        {
            // 1) 绑定并启用 DataAnnotations/启动校验
            var builder = services
                .AddOptions<T>()
                .Bind(section, o => { /* 可选 */ o.BindNonPublicProperties = false; });

            if (attr.ValidateDataAnnotations)
                builder = builder.ValidateDataAnnotations();

            if (attr.ValidateOnStart)
                builder = builder.ValidateOnStart();

            // 2) 明确添加“配置变更”触发源（多数情况下 .Bind 已添加，这里显式保证）
            services.AddSingleton<IOptionsChangeTokenSource<T>>(
                sp => new ConfigurationChangeTokenSource<T>(section));

            // 3) 再次显式配置一次，确保 Monitor 每次重建值时都基于当前 section 绑定
            services.AddSingleton<IConfigureOptions<T>>(
                sp => new NamedConfigureFromConfigurationOptions<T>(
                    Options.DefaultName, section,
                    binderOptions => { /* 同上 */ binderOptions.BindNonPublicProperties = false; }
                ));
        }

        // 直接注入强类型单例（不热更新）
        private static void AddSingletonBound<T>(
            IServiceCollection services,
            IConfiguration section,
            ConfigSectionAttribute attr) where T : class, new()
        {
            var instance = section.Get<T>() ?? new T();

            // 如果需要 DataAnnotations 校验
            if (attr.ValidateDataAnnotations)
            {
                var validation = new DataAnnotationsValidator();
                validation.ValidateObjectRecursive(instance, throwOnError: true);
            }

            services.AddSingleton(instance);
            // 使用： 构造函数注入 T 本身
        }

        // 简单 DataAnnotations 校验器（可放到单独文件）
        private sealed class DataAnnotationsValidator
        {
            public void ValidateObjectRecursive(object obj, bool throwOnError)
            {
                var ctx = new ValidationContext(obj);
                var results = new List<ValidationResult>();
                var valid = Validator.TryValidateObject(obj, ctx, results, validateAllProperties: true);

                if (!valid && throwOnError)
                    throw new OptionsValidationException(obj.GetType().Name, obj.GetType(), results.Select(r => r.ErrorMessage!));
            }
        }
    }
}

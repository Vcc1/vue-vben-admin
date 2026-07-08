using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.IOC
{
    public static class ServiceProvider
    {
        /// <summary>
        /// 以一个独立的Service容器作用域运行
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        public static async Task RunAsync<T>(this IServiceProvider Services, Func<T, Task> action)
        {
            using (var scope = Services.CreateScope())
            {
                var t = scope.ServiceProvider.GetRequiredService<T>();
                await action(t); ;
            }
        }
        public static void Run<T>(this IServiceProvider Services, Action<T> action)
        {
            using (var scope = Services.CreateScope())
            {
                var t = scope.ServiceProvider.GetRequiredService<T>();
                action(t); ;
            }
        }



        public static async Task<Object> RunAsync<T>(this IServiceProvider Services, Func<T, Task<Object>> action)
        {
            using (var scope = Services.CreateScope())
            {
                var t = scope.ServiceProvider.GetRequiredService<T>();
                return await action(t); ;
            }
        }

        public static Object Run<T>(this IServiceProvider Services, Func<T, Object> action)
        {
            using (var scope = Services.CreateScope())
            {
                var t = scope.ServiceProvider.GetRequiredService<T>();
                return action(t); ;
            }
        }

       
    }
}

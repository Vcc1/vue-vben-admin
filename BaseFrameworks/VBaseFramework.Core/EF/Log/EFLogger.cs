using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VBaseFramework.Core.EF.Log
{
    public class EFLogger : ILogger
    {
        private readonly string categoryName;
        private readonly ILogger<EFLogger> _Logger;

        public EFLogger(string categoryName, ILogger<EFLogger> logger)
        {
            this.categoryName = categoryName;
            _Logger = logger;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
    Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!_Logger.IsEnabled(LogLevel.Debug) || categoryName != "Microsoft.EntityFrameworkCore.Database.Command")
                return;

            var logContent = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(logContent)) return;

            string prefix = null;

            if (logContent.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                prefix = "EF SELECT";
            else if (logContent.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
                prefix = "EF INSERT";
            else if (logContent.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
                prefix = "EF UPDATE";
            else if (logContent.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
                prefix = "EF DELETE";
            else
                return; // 非目标 SQL 类型，不处理

            _Logger.LogTrace("====== {0} ======\r\n{1}", prefix, logContent);




            // 异常处理
            if (exception != null)
            {
                _Logger.LogError(exception, "❌ 执行异常");
            }
        }

     
        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }
    }
}

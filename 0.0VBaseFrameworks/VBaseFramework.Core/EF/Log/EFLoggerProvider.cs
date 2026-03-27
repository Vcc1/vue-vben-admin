using Framework.Core.IOC;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.EF.Log
{
    [Service]
    public class EFLoggerProvider : ILoggerProvider
    {
        private readonly ILogger<EFLogger> _injectedLogger;

        public EFLoggerProvider(ILogger<EFLogger> injectedLogger)
        {
            _injectedLogger = injectedLogger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new EFLogger(categoryName, _injectedLogger);
        }

        public void Dispose() { }
    }


}

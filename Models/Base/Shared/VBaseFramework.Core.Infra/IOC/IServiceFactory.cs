using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.IOC
{
    public interface IServiceFactory
    {
        object CreateInstance(IServiceProvider serviceProvider);
    }
    public interface IServiceFactory<T> : IServiceFactory
    {

    }

}

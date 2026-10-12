using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.EF
{
    public class EFDbServiceAttribute : Attribute
    {
        public string ConfigKey { get; }

        public EFDbServiceAttribute(string configKey)
        {
            ConfigKey = configKey;
        }
    }
}

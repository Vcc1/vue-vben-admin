using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.IOC
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ConfigSectionAttribute : Attribute
    {
        public string Section { get; }
        public bool Reloadable { get; init; } = true;          // true=Options; false=Singleton
        public bool ValidateDataAnnotations { get; init; } = false;
        public bool ValidateOnStart { get; init; } = false;

        public ConfigSectionAttribute(string section)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
        }
    }
}

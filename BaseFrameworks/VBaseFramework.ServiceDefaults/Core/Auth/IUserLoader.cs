using Framework.Core.IOC;

namespace VBaseFramework.ServiceDefaults.Core.Auth
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public interface IUserLoader
    {
        public int LoadUserIdForAppId(string AppId);
    }
}
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace AdminIdentity.Infra;

/// <summary>
/// AdminIdentity 专用的 Redis 上下文。沿用仓库约定：继承 <see cref="RedisContextBase"/>，
/// 连接串取 <c>ConnectionStrings:AdminIdentityRedis</c>，库序号由连接串里的 <c>defaultDatabase=</c> 指定。
/// </summary>
internal sealed class AdminIdentityRedisContext(IConfiguration configuration)
    : RedisContextBase(configuration, AdminIdentityRedisContext.ConnectionKey)
{
    public const string ConnectionKey = "AdminIdentityRedis";

    private const string KeyPrefix = "AdminIdentity:";

    public IDatabase Database => DB;

    public static string SessionKey(string sid) => $"{KeyPrefix}Session:{sid}";

    /// <summary>吊销标记单独一个 key：只写不删，避免与并发读请求回填事实缓存相互覆盖。</summary>
    public static string SessionRevokedKey(string sid) => $"{KeyPrefix}SessionRevoked:{sid}";

    public static string StepUpKey(string key) => $"{KeyPrefix}StepUp:{key}";

    public static string LoginFailureKey(string userName) => $"{KeyPrefix}LoginFailure:{userName}";

    public static string CaptchaTokenKey(string captchaToken) => $"{KeyPrefix}Captcha:{captchaToken}";

    public static string ImageCaptchaKey(string captchaId) => $"{KeyPrefix}ImageCaptcha:{captchaId}";

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConnectionKey));
}

using AdminIdentity.App;
using Microsoft.AspNetCore.Http;

namespace AdminIdentity.Infra;

/// <summary>
/// deviceId 由授权服务器侧 cookie 承载，前端不需要（也不应该）自己生成再上报。
/// 刻意不设 HttpOnly，因为"我的设备"页面要展示当前设备标识；
/// 生产是 HTTP，所以也不能设 Secure。
/// 注意：cookie 可被用户手工篡改，deviceId 只是体验层（决定要不要二次验证），不是安全边界。
/// </summary>
internal static class DeviceCookie
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(3650);

    public static string? Read(HttpContext? context) =>
        context?.Request.Cookies.TryGetValue(DeviceCookieName.Value, out var value) == true && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    /// <summary>读取当前 deviceId；首次访问时生成并写回响应 cookie。</summary>
    public static string? Ensure(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        if (Read(context) is { } existing)
        {
            return existing;
        }

        var deviceId = "dev_" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        context.Response.Cookies.Append(DeviceCookieName.Value, deviceId, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(Lifetime),
            Path = "/"
        });
        return deviceId;
    }
}

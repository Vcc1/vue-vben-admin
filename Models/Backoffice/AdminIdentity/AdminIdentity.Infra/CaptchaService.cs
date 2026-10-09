using System.Security.Cryptography;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AdminIdentity.Infra;

internal sealed class CaptchaService(
    AdminDbContext dbContext,
    ICaptchaFailureCounter failureCounter,
    ICaptchaTokenStore tokenStore,
    IImageCaptchaAnswerStore imageCaptchaStore,
    CaptchaImageGenerator imageGenerator) : ICaptchaService
{
    private static readonly TimeSpan FailureTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ImageCaptchaTtl = TimeSpan.FromMinutes(3);
    private const int FailureThreshold = 3;

    public async Task<bool> IsCaptchaRequiredAsync(Guid? userId, string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return true;
        }

        var deviceKnown = await dbContext.TrustedDevices.AsNoTracking()
            .AnyAsync(d => d.DeviceId == deviceId, cancellationToken);
        if (!deviceKnown)
        {
            return true;
        }

        if (userId is null)
        {
            return false;
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var failures = await failureCounter.GetAsync(user.UserName!, cancellationToken);
        return failures >= FailureThreshold;
    }

    public async Task<bool> IsCaptchaRequiredByUserAsync(string? userName, string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return await IsCaptchaRequiredAsync(null, deviceId, cancellationToken);
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        return await IsCaptchaRequiredAsync(user?.Id, deviceId, cancellationToken);
    }

    public Task<int> GetFailureCountAsync(string userName, CancellationToken cancellationToken)
        => failureCounter.GetAsync(userName, cancellationToken);

    public Task IncrementFailureAsync(string userName, CancellationToken cancellationToken)
        => failureCounter.IncrementAsync(userName, FailureTtl, cancellationToken);

    public Task ResetFailureAsync(string userName, CancellationToken cancellationToken)
        => failureCounter.ResetAsync(userName, cancellationToken);

    public Task<string> IssueTokenAsync(string deviceId, string userName, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        return tokenStore.SetAsync(token, deviceId, userName, TokenTtl, cancellationToken);
    }

    public Task<string> IssueTokenWithDeviceAsync(string? existingDeviceId, string userName,
        Action<string> setDeviceCookie, CancellationToken cancellationToken)
    {
        var deviceId = existingDeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = "dev_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            setDeviceCookie(deviceId);
        }
        return IssueTokenAsync(deviceId, userName, cancellationToken);
    }

    public Task<bool> ConsumeTokenAsync(string deviceId, string userName, string captchaToken, CancellationToken cancellationToken)
        => tokenStore.ConsumeAsync(captchaToken, deviceId, userName, cancellationToken);

    public async Task<ImageCaptchaResponse> GenerateImageCaptchaAsync(CancellationToken cancellationToken)
    {
        var (code, pngBytes) = imageGenerator.Generate();
        var captchaId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        await imageCaptchaStore.SetAsync(captchaId, code, ImageCaptchaTtl, cancellationToken);
        var base64 = Convert.ToBase64String(pngBytes);
        return new ImageCaptchaResponse(captchaId, base64);
    }

    public async Task<bool> ValidateImageCaptchaAsync(string captchaId, string answer, CancellationToken cancellationToken)
    {
        var expected = await imageCaptchaStore.GetAndConsumeAsync(captchaId, cancellationToken);
        if (expected is null) return false;
        return string.Equals(expected, answer.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

#region Failure Counter

internal interface ICaptchaFailureCounter
{
    Task<int> GetAsync(string userName, CancellationToken cancellationToken);
    Task IncrementAsync(string userName, TimeSpan ttl, CancellationToken cancellationToken);
    Task ResetAsync(string userName, CancellationToken cancellationToken);
}

internal sealed class RedisCaptchaFailureCounter(
    AdminIdentityRedisContext redis,
    ILogger<RedisCaptchaFailureCounter> logger) : ICaptchaFailureCounter
{
    public async Task<int> GetAsync(string userName, CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.Database.StringGetAsync(AdminIdentityRedisContext.LoginFailureKey(userName));
            return value.IsNullOrEmpty ? 0 : (int)value;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "读取登录失败计数失败，视为 0。user={User}", userName);
            return 0;
        }
    }

    public async Task IncrementAsync(string userName, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var key = AdminIdentityRedisContext.LoginFailureKey(userName);
            var count = await redis.Database.StringIncrementAsync(key);
            if (count == 1)
            {
                await redis.Database.KeyExpireAsync(key, ttl);
            }
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "递增登录失败计数失败。user={User}", userName);
        }
    }

    public async Task ResetAsync(string userName, CancellationToken cancellationToken)
    {
        try
        {
            await redis.Database.KeyDeleteAsync(AdminIdentityRedisContext.LoginFailureKey(userName));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "重置登录失败计数失败。user={User}", userName);
        }
    }
}

internal sealed class MemoryCaptchaFailureCounter : ICaptchaFailureCounter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, DateTime ExpiresAt)> Store = new(StringComparer.OrdinalIgnoreCase);

    public Task<int> GetAsync(string userName, CancellationToken cancellationToken)
    {
        if (Store.TryGetValue(userName, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult(entry.Count);
        }
        Store.TryRemove(userName, out _);
        return Task.FromResult(0);
    }

    public Task IncrementAsync(string userName, TimeSpan ttl, CancellationToken cancellationToken)
    {
        Store.AddOrUpdate(userName,
            _ => (1, DateTime.UtcNow + ttl),
            (_, existing) => existing.ExpiresAt > DateTime.UtcNow
                ? (existing.Count + 1, existing.ExpiresAt)
                : (1, DateTime.UtcNow + ttl));
        return Task.CompletedTask;
    }

    public Task ResetAsync(string userName, CancellationToken cancellationToken)
    {
        Store.TryRemove(userName, out _);
        return Task.CompletedTask;
    }
}

#endregion

#region Token Store

internal interface ICaptchaTokenStore
{
    Task<string> SetAsync(string token, string deviceId, string userName, TimeSpan ttl, CancellationToken cancellationToken);
    Task<bool> ConsumeAsync(string token, string deviceId, string userName, CancellationToken cancellationToken);
}

internal sealed class RedisCaptchaTokenStore(
    AdminIdentityRedisContext redis,
    ILogger<RedisCaptchaTokenStore> logger) : ICaptchaTokenStore
{
    public async Task<string> SetAsync(string token, string deviceId, string userName, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var value = System.Text.Json.JsonSerializer.Serialize(new { deviceId, userName });
            await redis.Database.StringSetAsync(
                AdminIdentityRedisContext.CaptchaTokenKey(token), value, ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "写入 CAPTCHA token 失败。");
        }
        return token;
    }

    public async Task<bool> ConsumeAsync(string token, string deviceId, string userName, CancellationToken cancellationToken)
    {
        try
        {
            var key = AdminIdentityRedisContext.CaptchaTokenKey(token);
            var value = await redis.Database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                return false;
            }

            var doc = System.Text.Json.JsonDocument.Parse(value.ToString());
            var storedDevice = doc.RootElement.GetProperty("deviceId").GetString();
            var storedUser = doc.RootElement.GetProperty("userName").GetString();

            if (!string.Equals(storedDevice, deviceId, StringComparison.Ordinal) ||
                !string.Equals(storedUser, userName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            await redis.Database.KeyDeleteAsync(key);
            return true;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "验证 CAPTCHA token 失败。");
            return false;
        }
    }
}

internal sealed class MemoryCaptchaTokenStore : ICaptchaTokenStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string DeviceId, string UserName, DateTime ExpiresAt)> Store = new(StringComparer.Ordinal);

    public Task<string> SetAsync(string token, string deviceId, string userName, TimeSpan ttl, CancellationToken cancellationToken)
    {
        Store[token] = (deviceId, userName, DateTime.UtcNow + ttl);
        return Task.FromResult(token);
    }

    public Task<bool> ConsumeAsync(string token, string deviceId, string userName, CancellationToken cancellationToken)
    {
        if (!Store.TryRemove(token, out var entry))
        {
            return Task.FromResult(false);
        }
        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            return Task.FromResult(false);
        }
        var match = string.Equals(entry.DeviceId, deviceId, StringComparison.Ordinal) &&
                     string.Equals(entry.UserName, userName, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(match);
    }
}

#endregion

#region Image Captcha Answer Store

internal interface IImageCaptchaAnswerStore
{
    Task SetAsync(string captchaId, string answer, TimeSpan ttl, CancellationToken cancellationToken);
    Task<string?> GetAndConsumeAsync(string captchaId, CancellationToken cancellationToken);
}

internal sealed class RedisImageCaptchaAnswerStore(
    AdminIdentityRedisContext redis,
    ILogger<RedisImageCaptchaAnswerStore> logger) : IImageCaptchaAnswerStore
{
    public async Task SetAsync(string captchaId, string answer, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            await redis.Database.StringSetAsync(
                AdminIdentityRedisContext.ImageCaptchaKey(captchaId), answer, ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "写入图片验证码答案失败。captchaId={CaptchaId}", captchaId);
        }
    }

    public async Task<string?> GetAndConsumeAsync(string captchaId, CancellationToken cancellationToken)
    {
        try
        {
            var key = AdminIdentityRedisContext.ImageCaptchaKey(captchaId);
            var value = await redis.Database.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            await redis.Database.KeyDeleteAsync(key);
            return value.ToString();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "验证图片验证码答案失败。captchaId={CaptchaId}", captchaId);
            return null;
        }
    }
}

internal sealed class MemoryImageCaptchaAnswerStore : IImageCaptchaAnswerStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Answer, DateTime ExpiresAt)> Store = new(StringComparer.Ordinal);

    public Task SetAsync(string captchaId, string answer, TimeSpan ttl, CancellationToken cancellationToken)
    {
        Store[captchaId] = (answer, DateTime.UtcNow + ttl);
        return Task.CompletedTask;
    }

    public Task<string?> GetAndConsumeAsync(string captchaId, CancellationToken cancellationToken)
    {
        if (!Store.TryRemove(captchaId, out var entry))
        {
            return Task.FromResult<string?>(null);
        }
        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            return Task.FromResult<string?>(null);
        }
        return Task.FromResult<string?>(entry.Answer);
    }
}

#endregion

using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 按供应商控制查价请求启动频率的限流器。
/// 每个供应商使用独立的 FixedWindowRateLimiter，避免一个供应商的限流影响其他供应商。
/// </summary>
public sealed class SupplierQueryPriceRateLimiter : IDisposable
{
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ConcurrentDictionary<int, RateLimiter> _rateLimiters = new();

    public SupplierQueryPriceRateLimiter(IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public async ValueTask<IDisposable> WaitAsync(int supId, CancellationToken cancellationToken)
    {
        var rateLimit = ResolveRateLimitPerSecond(supId);
        if (rateLimit <= 0)
        {
            return NoopLease.Instance;
        }

        var rateLimiter = _rateLimiters.GetOrAdd(supId, _ => CreateRateLimiter(rateLimit));
        var lease = await rateLimiter.AcquireAsync(1, cancellationToken);
        if (lease.IsAcquired)
        {
            return lease;
        }

        lease.Dispose();
        throw new InvalidOperationException($"Query price request rate limit queue rejected. SupId={supId}");
    }

    public void Dispose()
    {
        foreach (var rateLimiter in _rateLimiters.Values)
        {
            rateLimiter.Dispose();
        }
    }

    int ResolveRateLimitPerSecond(int supId)
    {
        return _optionsMonitor.CurrentValue.SupRateLimits.TryGetValue(supId, out var rateLimit)
            ? Math.Max(0, rateLimit)
            : 0;
    }

    static RateLimiter CreateRateLimiter(int rateLimit)
    {
        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, rateLimit),
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    sealed class NoopLease : IDisposable
    {
        public static readonly NoopLease Instance = new();

        NoopLease()
        {
        }

        public void Dispose()
        {
        }
    }
}

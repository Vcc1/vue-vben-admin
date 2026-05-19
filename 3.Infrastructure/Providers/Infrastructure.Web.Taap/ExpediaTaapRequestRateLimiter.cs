using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapRequestRateLimiter : IDisposable
{
    private readonly IOptionsMonitor<ExpediaTaapOptions> _optionsMonitor;
    private readonly ConcurrentDictionary<int, RateLimiter> _rateLimiters = new();

    public ExpediaTaapRequestRateLimiter(IOptionsMonitor<ExpediaTaapOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public ValueTask<RateLimitLease> WaitAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var rateLimiter = _rateLimiters.GetOrAdd(accountId, _ => CreateRateLimiter());
        return rateLimiter.AcquireAsync(1, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var rateLimiter in _rateLimiters.Values)
        {
            rateLimiter.Dispose();
        }
    }

    private RateLimiter CreateRateLimiter()
    {
        var options = _optionsMonitor.CurrentValue;
        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(options.RequestsPerSecond, 1),
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = Math.Max(options.RequestQueueLimit, 0),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }
}

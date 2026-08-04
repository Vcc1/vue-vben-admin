using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Host.HotelMappingSync;

namespace Tests.Host.Tests;

public sealed class HotelMappingRedisSyncWorkerTests
{
    [Fact]
    public void TryStart_RejectsAnotherRequestWhileRunning()
    {
        var worker = new HotelMappingRedisSyncWorker(
            new UnusedScopeFactory(),
            NullLogger<HotelMappingRedisSyncWorker>.Instance);

        Assert.True(worker.TryStart(out var started));
        Assert.Equal("running", started.State);

        Assert.False(worker.TryStart(out var rejected));
        Assert.Equal("running", rejected.State);
        Assert.Equal(started.StartedAt, rejected.StartedAt);
    }

    sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => throw new InvalidOperationException("The worker is not started in this test.");
    }
}

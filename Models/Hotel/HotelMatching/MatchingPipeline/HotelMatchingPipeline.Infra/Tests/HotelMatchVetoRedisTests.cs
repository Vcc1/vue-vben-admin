using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using Infrastructure.Redis.HotelMatchingRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class HotelMatchVetoRedisTests
{
    [Fact]
    public void Pair_removal_preserves_other_supplier_hotels()
    {
        var values = new[]
        {
            Hotel(51, "SUP-1"),
            Hotel(51, "SUP-2"),
            Hotel(52, "SUP-1")
        };

        var remaining = HotelMappingRedisStore.RemoveSupplierHotel(values, 51, "SUP-1");

        Assert.Equal(2, remaining.Length);
        Assert.Contains(remaining, hotel => hotel.SupId == 51 && hotel.SHotelCode == "SUP-2");
        Assert.Contains(remaining, hotel => hotel.SupId == 52 && hotel.SHotelCode == "SUP-1");
    }

    [Fact]
    public async Task Worker_alerts_once_fast_retries_and_sends_recovery()
    {
        var repository = new RetryRepository(Item());
        var synchronizer = new FailOnceSynchronizer();
        var notifier = new RecordingNotifier();
        var signal = new HotelMatchVetoSyncSignal();
        var services = new ServiceCollection()
            .AddSingleton<IHotelMatchVetoRepository>(repository)
            .BuildServiceProvider();
        var worker = new HotelMatchVetoRedisSyncWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            synchronizer,
            notifier,
            signal,
            new StaticOptionsMonitor<HotelMatchVetoOptions>(new HotelMatchVetoOptions
            {
                RetryDelaySeconds = 10
            }),
            NullLogger<HotelMatchVetoRedisSyncWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await notifier.FailureSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await notifier.RecoverySent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, synchronizer.AttemptCount);
        Assert.Equal(1, notifier.FailureCount);
        Assert.Equal(1, notifier.RecoveryCount);
        Assert.NotNull(repository.Current.RedisSyncedAt);
        Assert.NotNull(repository.Current.RecoveryAlertSentAt);
    }

    static SupHotelInfoDto Hotel(int supId, string supplierHotelCode)
        => new()
        {
            SupId = supId,
            SHotelCode = supplierHotelCode
        };

    static HotelMatchVetoSyncItem Item()
    {
        var now = DateTimeOffset.Now;
        return new HotelMatchVetoSyncItem(
            1,
            10,
            51,
            "SUP-1",
            "MT-1",
            "operator",
            "incorrect match",
            0,
            now,
            now,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    sealed class FailOnceSynchronizer : IHotelMatchVetoRedisSynchronizer
    {
        public int AttemptCount { get; private set; }

        public Task RemoveAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken)
        {
            AttemptCount++;
            return AttemptCount == 1
                ? Task.FromException(new InvalidOperationException("redis unavailable"))
                : Task.CompletedTask;
        }
    }

    sealed class RecordingNotifier : IHotelMatchVetoNotifier
    {
        public TaskCompletionSource FailureSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RecoverySent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int FailureCount { get; private set; }
        public int RecoveryCount { get; private set; }

        public Task SendFailureAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken)
        {
            FailureCount++;
            FailureSent.TrySetResult();
            return Task.CompletedTask;
        }

        public Task SendRecoveryAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken)
        {
            RecoveryCount++;
            RecoverySent.TrySetResult();
            return Task.CompletedTask;
        }
    }

    sealed class RetryRepository
        (HotelMatchVetoSyncItem initial) : IHotelMatchVetoRepository
    {
        readonly object _gate = new();
        HotelMatchVetoSyncItem _current = initial;

        public HotelMatchVetoSyncItem Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        public Task<IReadOnlyList<HotelMatchVetoSyncItem>> LoadPendingSyncAsync(
            int batchSize,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<HotelMatchVetoSyncItem>>([Current]);

        public Task<HotelMatchVetoSyncItem> RecordRedisFailureAsync(
            long vetoId,
            string error,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _current = _current with
                {
                    RedisSyncAttemptCount = _current.RedisSyncAttemptCount + 1,
                    FirstRedisSyncFailureAt = _current.FirstRedisSyncFailureAt ?? failedAt,
                    LastRedisSyncAttemptAt = failedAt,
                    LastRedisSyncError = error
                };
                return Task.FromResult(_current);
            }
        }

        public Task MarkFailureAlertSentAsync(long vetoId, DateTimeOffset sentAt, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _current = _current with { FailureAlertSentAt = sentAt };
                return Task.CompletedTask;
            }
        }

        public Task<HotelMatchVetoSyncItem> MarkRedisSyncedAsync(long vetoId, DateTimeOffset syncedAt, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _current = _current with
                {
                    RedisSyncAttemptCount = _current.RedisSyncAttemptCount + 1,
                    LastRedisSyncAttemptAt = syncedAt,
                    LastRedisSyncError = null,
                    RedisSyncedAt = syncedAt
                };
                return Task.FromResult(_current);
            }
        }

        public Task MarkRecoveryAlertSentAsync(long vetoId, DateTimeOffset sentAt, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _current = _current with { RecoveryAlertSentAt = sentAt };
                return Task.CompletedTask;
            }
        }

        public Task<HotelMatchVetoCreateResult> CreateAsync(HotelMatchVetoRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<HotelMatchVetoSyncItem?> GetAsync(long vetoId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> IsVetoedAsync(int supId, string supplierHotelCode, string mtHotelCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

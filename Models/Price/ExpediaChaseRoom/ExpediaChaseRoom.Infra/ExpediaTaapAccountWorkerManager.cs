using System.Collections.Concurrent;
using System.Threading.Channels;
using Infrastructure.Sql.SupCacheDB;
using Infrastructure.Web.Taap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaTaapAccountWorkerManager : BackgroundService
{
    private static readonly TimeSpan AccountRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExpediaChaseRoomQueue _queue;
    private readonly ExpediaChaseRoomAppState _state;
    private readonly IExpediaChaseRoomTaskStore _taskStore;
    private readonly IExpediaTaapAccountQueryService _queryService;
    private readonly IExpediaTaapPromoLabelProvider _promoLabelProvider;
    private readonly IExpediaChaseRoomResultSink _resultSink;
    private readonly ExpediaChaseRoomMetrics _metrics;
    private readonly ILogger<ExpediaTaapAccountWorkerManager> _logger;
    private readonly ConcurrentDictionary<int, AccountWorkerGroup> _workers = new();

    public ExpediaTaapAccountWorkerManager(
        IServiceScopeFactory scopeFactory,
        ExpediaChaseRoomQueue queue,
        ExpediaChaseRoomAppState state,
        IExpediaChaseRoomTaskStore taskStore,
        IExpediaTaapAccountQueryService queryService,
        IExpediaTaapPromoLabelProvider promoLabelProvider,
        IExpediaChaseRoomResultSink resultSink,
        ExpediaChaseRoomMetrics metrics,
        ILogger<ExpediaTaapAccountWorkerManager> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _state = state;
        _taskStore = taskStore;
        _queryService = queryService;
        _promoLabelProvider = promoLabelProvider;
        _resultSink = resultSink;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncWorkersSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(AccountRefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncWorkersSafelyAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var group in _workers.Values)
        {
            foreach (var worker in group.Workers)
            {
                worker.Cancellation.Cancel();
            }
        }

        try
        {
            await Task.WhenAll(_workers.Values.SelectMany(x => x.Workers).Select(x => x.Task));
        }
        catch
        {
            // Worker tasks already log their own failures.
        }
        finally
        {
            foreach (var group in _workers.Values)
            {
                foreach (var worker in group.Workers)
                {
                    worker.Cancellation.Dispose();
                }
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task SyncWorkersSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SyncWorkersAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 Expedia TAAP 账号 Worker 失败。");
        }
    }

    private async Task SyncWorkersAsync(CancellationToken cancellationToken)
    {
        var accounts = await LoadEnabledAccountsAsync(cancellationToken);
        var accountMap = accounts.ToDictionary(x => x.AccountId);

        foreach (var entry in _workers.ToArray())
        {
            if (!accountMap.TryGetValue(entry.Key, out var account))
            {
                await StopWorkerGroupAsync(entry.Key, entry.Value, "账号被停用或删除");
                continue;
            }

            if (entry.Value.Workers.Any(x => x.Task.IsCompleted))
            {
                await StopWorkerGroupAsync(entry.Key, entry.Value, "存在已退出的并发消费者");
                continue;
            }

            if (entry.Value.Registration != account)
            {
                await StopWorkerGroupAsync(entry.Key, entry.Value, "账号配置发生变化");
            }
        }

        foreach (var account in accounts)
        {
            if (_workers.ContainsKey(account.AccountId))
            {
                continue;
            }

            StartWorkerGroup(account, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<AccountRegistration>> LoadEnabledAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var accounts = await dbContext.ExpediaTaapAccounts
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Id)
            .Select(x => new AccountRegistration(
                x.Id,
                x.AccountName,
                x.AccountLevelId,
                x.SigninEmailAddress,
                x.SigninPassword,
                x.WorkerConcurrency))
            .ToListAsync(cancellationToken);

        await RefreshPromoLabelsAsync(dbContext, accounts, cancellationToken);

        foreach (var invalidAccount in accounts.Where(x =>
                     string.IsNullOrWhiteSpace(x.SigninEmailAddress) ||
                     string.IsNullOrWhiteSpace(x.SigninPassword)))
        {
            _logger.LogWarning(
                "Expedia TAAP 账号 {AccountName} 因登录凭据不完整，已跳过启动任务消费者。",
                invalidAccount.AccountName);
        }

        foreach (var invalidAccount in accounts.Where(x => x.WorkerConcurrency <= 0))
        {
            _logger.LogWarning(
                "Expedia TAAP 账号 {AccountName} 的并发数为 {WorkerConcurrency}，已跳过启动任务消费者。",
                invalidAccount.AccountName,
                invalidAccount.WorkerConcurrency);
        }

        return accounts
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.SigninEmailAddress) &&
                !string.IsNullOrWhiteSpace(x.SigninPassword) &&
                x.WorkerConcurrency > 0)
            .ToList();
    }

    private async Task RefreshPromoLabelsAsync(
        SupCacheDbContext dbContext,
        IReadOnlyList<AccountRegistration> accounts,
        CancellationToken cancellationToken)
    {
        var levelIds = accounts
            .Select(x => x.AccountLevelId)
            .Distinct()
            .ToArray();

        var rateRows = await dbContext.ExpediaTaapAccountLevelCommissionRates
            .AsNoTracking()
            .Where(x => levelIds.Contains(x.AccountLevelId) && x.IsEnabled)
            .OrderBy(x => x.AccountLevelId)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var promoLabelsByLevel = rateRows
            .GroupBy(x => x.AccountLevelId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, decimal>)group
                    .GroupBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        item => item.Key,
                        item => item.Last().CommissionRate,
                        StringComparer.OrdinalIgnoreCase));

        var promoLabelsByAccount = accounts.ToDictionary(
            account => account.AccountId,
            account => promoLabelsByLevel.TryGetValue(account.AccountLevelId, out var promoLabels)
                ? promoLabels
                : (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));

        _promoLabelProvider.Replace(promoLabelsByAccount);
    }

    private void StartWorkerGroup(AccountRegistration registration, CancellationToken stoppingToken)
    {
        var workers = new List<AccountWorkerHandle>(registration.WorkerConcurrency);
        for (var workerSlot = 1; workerSlot <= registration.WorkerConcurrency; workerSlot++)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var slot = workerSlot;
            var workerTask = RunAccountWorkerAsync(registration, slot, linkedCts.Token);
            workers.Add(new AccountWorkerHandle(slot, linkedCts, workerTask));
        }

        _workers[registration.AccountId] = new AccountWorkerGroup(registration, workers);
        _metrics.SetAccountWorkerCount(registration.AccountName, registration.WorkerConcurrency);

        _logger.LogInformation(
            "已启动 Expedia TAAP 账号任务消费者。账号：{AccountName}，邮箱：{SigninEmailAddress}，并发数：{WorkerConcurrency}。",
            registration.AccountName,
            registration.SigninEmailAddress,
            registration.WorkerConcurrency);
    }

    private async Task StopWorkerGroupAsync(int accountId, AccountWorkerGroup group, string reason)
    {
        if (!_workers.TryRemove(accountId, out _))
        {
            return;
        }

        foreach (var worker in group.Workers)
        {
            worker.Cancellation.Cancel();
        }

        try
        {
            await Task.WhenAll(group.Workers.Select(x => x.Task));
        }
        catch
        {
            // Worker tasks already log their own failures.
        }
        finally
        {
            foreach (var worker in group.Workers)
            {
                worker.Cancellation.Dispose();
            }
        }

        _metrics.SetAccountWorkerCount(group.Registration.AccountName, 0);

        _logger.LogInformation(
            "已停止 Expedia TAAP 账号任务消费者。账号 Id：{AccountId}，原因：{Reason}。",
            accountId,
            reason);
    }

    private async Task RunAccountWorkerAsync(
        AccountRegistration registration,
        int workerSlot,
        CancellationToken cancellationToken)
    {
        var account = registration.ToWorkerContext(workerSlot);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ExpediaChaseRoomTask? task = null;
                var countedInProgress = false;

                try
                {
                    task = await _queue.Channel.Reader.ReadAsync(cancellationToken);
                    _state.DecreaseQueueRemaining();
                    _state.IncreaseInProgress();
                    _metrics.IncreaseAccountInProgress(account.AccountName);
                    countedInProgress = true;

                    //await _taskStore.AssignProcessingTaskAsync(task.Id, account.WorkerKey, cancellationToken);

                    _logger.LogInformation(
                        "Expedia 追房任务开始处理。账号：{AccountName}，并发槽位：{WorkerSlot}，酒店：{PropertyId}，入住：{CheckIn:yyyy-MM-dd}，离店：{CheckOut:yyyy-MM-dd}，房间数：{Rooms}，成人数：{Adults}。",
                        account.AccountName,
                        account.WorkerSlot,
                        task.PropertyId,
                        task.CheckIn,
                        task.CheckOut,
                        task.Rooms,
                        task.Adults);

                    var result = await _queryService.QueryPricesAsync(
                        account,
                        new ExpediaTaapPriceQueryRequest
                        {
                            PropertyId = task.PropertyId,
                            CheckIn = DateOnly.FromDateTime(task.CheckIn),
                            CheckOut = DateOnly.FromDateTime(task.CheckOut),
                            Rooms = task.Rooms,
                            Adults = task.Adults
                        },
                        cancellationToken);

                    await _resultSink.OnSuccessAsync(account, task, result, cancellationToken);
                    _state.IncreaseCompleted();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (task is not null)
                    {
                        await TryReleaseTaskAsync(task.Id);
                    }

                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (task is not null)
                    {
                        await TryReleaseTaskAsync(task.Id);
                        await _resultSink.OnErrorAsync(account, task, ex, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogError(
                            ex,
                            "Expedia TAAP 账号任务消费者在获取任务前发生异常。账号：{AccountName}，并发槽位：{WorkerSlot}。",
                            account.AccountName,
                            account.WorkerSlot);
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                finally
                {
                    if (countedInProgress)
                    {
                        _state.DecreaseInProgress();
                        _metrics.DecreaseAccountInProgress(account.AccountName);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Expedia TAAP 账号任务消费者异常退出。账号 Id：{AccountId}，并发槽位：{WorkerSlot}。",
                account.AccountId,
                account.WorkerSlot);
            throw;
        }
    }

    private async Task TryReleaseTaskAsync(int taskId)
    {
        try
        {
            await _taskStore.ReleaseTaskAsync(taskId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "将 Expedia 追房任务 {TaskId} 释放回待处理状态失败。", taskId);
        }
    }

    private sealed record AccountRegistration(
        int AccountId,
        string AccountName,
        int AccountLevelId,
        string SigninEmailAddress,
        string SigninPassword,
        int WorkerConcurrency)
    {
        public ExpediaTaapAccountWorkerContext ToWorkerContext(int workerSlot)
        {
            return new ExpediaTaapAccountWorkerContext
            {
                AccountId = AccountId,
                AccountName = AccountName,
                SigninEmailAddress = SigninEmailAddress,
                WorkerSlot = workerSlot
            };
        }
    }

    private sealed record AccountWorkerHandle(
        int WorkerSlot,
        CancellationTokenSource Cancellation,
        Task Task);

    private sealed record AccountWorkerGroup(
        AccountRegistration Registration,
        IReadOnlyList<AccountWorkerHandle> Workers);
}

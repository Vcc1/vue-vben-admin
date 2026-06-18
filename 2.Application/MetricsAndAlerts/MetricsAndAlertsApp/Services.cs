using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetricsAndAlertsApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsAndAlertsApp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<PrometheusOptions>()
            .Bind(configuration.GetSection("Prometheus"))
            .ValidateOnStart();

        services
            .AddOptions<AlertOptions>()
            .Bind(configuration.GetSection("Alert"))
            .ValidateOnStart();

        services
            .AddOptions<AlertNotificationOptions>()
            .Bind(configuration.GetSection("AlertNotification"))
            .ValidateOnStart();

        services.AddScoped<MonitorTargetGroupService>();
        services.AddScoped<MonitorTargetService>();
        services.AddScoped<TargetAliveAlertRuleService>();
        services.AddScoped<TargetUrlMonitorRuleService>();
        services.AddScoped<AlertStateService>();
        services.AddScoped<AlertQueryService>();
        services.AddHostedService<TargetAliveAlertWorker>();
        services.AddHostedService<TargetUrlMonitorWorker>();
        return services;
    }
}

public sealed class MonitorTargetGroupService
{
    readonly IMetricsAndAlertsRepository _repository;

    public MonitorTargetGroupService(IMetricsAndAlertsRepository repository)
    {
        _repository = repository;
    }

    public async Task<MonitorTargetGroupDto> UpsertAsync(UpsertMonitorTargetGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.GroupName))
        {
            throw new ArgumentException("GroupName is required.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var group = await _repository.UpsertGroupAsync(new MonitorTargetGroup
        {
            Id = request.Id,
            GroupName = request.GroupName.Trim(),
            SortOrder = request.SortOrder,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return ToDto(group);
    }

    public async Task<IReadOnlyList<MonitorTargetGroupDto>> ListAsync(bool includeDisabled = false, CancellationToken cancellationToken = default)
    {
        var groups = await _repository.ListGroupsAsync(includeDisabled, cancellationToken);
        return groups.Select(ToDto).ToArray();
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _repository.DisableGroupAsync(id, cancellationToken);
    }

    static MonitorTargetGroupDto ToDto(MonitorTargetGroup group)
    {
        return new MonitorTargetGroupDto
        {
            Id = group.Id,
            GroupName = group.GroupName,
            SortOrder = group.SortOrder,
            IsEnabled = group.IsEnabled
        };
    }
}

public sealed class MonitorTargetService
{
    readonly IMetricsAndAlertsRepository _repository;
    readonly IPrometheusQueryClient _prometheus;

    public MonitorTargetService(
        IMetricsAndAlertsRepository repository,
        IPrometheusQueryClient prometheus)
    {
        _repository = repository;
        _prometheus = prometheus;
    }

    public async Task<MonitorTargetListItemDto> UpsertAsync(UpsertMonitorTargetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TargetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(request));
        }

        if (request.GroupId.HasValue && !await _repository.GroupExistsAsync(request.GroupId.Value, cancellationToken))
        {
            throw new ArgumentException($"GroupId {request.GroupId.Value} does not exist.", nameof(request));
        }

        var now = DateTime.UtcNow;
        await _repository.UpsertMonitorTargetAsync(new MonitorTarget
        {
            GroupId = request.GroupId,
            TargetKey = request.TargetKey.Trim(),
            TargetName = string.IsNullOrWhiteSpace(request.TargetName) ? null : request.TargetName.Trim(),
            TargetType = request.TargetType,
            SortOrder = request.SortOrder,
            NotifyUsers = MetricsAndAlertsUtilities.FormatNotifyUsers(request.NotifyUsers),
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        await UpsertOptionalAliveRuleAsync(request, now, cancellationToken);
        await UpsertOptionalCheckUrlAsync(request, now, cancellationToken);

        var list = await ListAsync(new MonitorTargetListQuery
        {
            TargetKeyEquals = request.TargetKey.Trim(),
            TargetType = request.TargetType
        }, cancellationToken);

        return list.First();
    }

    public async Task<IReadOnlyList<MonitorTargetListItemDto>> ListAsync(MonitorTargetListQuery query, CancellationToken cancellationToken = default)
    {
        var targets = (await _repository.ListMonitorTargetsAsync(query, cancellationToken)).ToList();
        foreach (var target in targets)
        {
            target.TargetTypeName = MetricsAndAlertsUtilities.GetTargetTypeName(target.TargetType);
            if (!target.AliveAlertEnabled || target.MinAliveCount is null)
            {
                continue;
            }

            try
            {
                target.OnlineCount = await _prometheus.GetOnlineCountAsync(
                    target.TargetKey,
                    target.TargetType,
                    offlineSeconds: target.OfflineSeconds ?? 60,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                target.OnlineCount = null;
                target.QueryError = ex.Message;
            }
        }

        return targets;
    }

    public Task<IReadOnlyList<MonitorTargetListItemDto>> ListApplicationsAsync(CancellationToken cancellationToken = default)
    {
        return ListAsync(new MonitorTargetListQuery
        {
            TargetType = MonitorTargetType.Application
        }, cancellationToken);
    }

    public Task<IReadOnlyList<TargetInstanceDto>> GetInstancesAsync(
        string targetKey,
        MonitorTargetType targetType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(targetKey));
        }

        return _prometheus.GetInstancesAsync(targetKey.Trim(), targetType, cancellationToken);
    }

    async Task UpsertOptionalAliveRuleAsync(UpsertMonitorTargetRequest request, DateTime now, CancellationToken cancellationToken)
    {
        if (!request.MinAliveCount.HasValue)
        {
            return;
        }

        var existing = await _repository.GetTargetAliveAlertRuleAsync(request.TargetKey.Trim(), request.TargetType, cancellationToken);
        await _repository.UpsertTargetAliveAlertRuleAsync(new TargetAliveAlertRule
        {
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            MinAliveCount = Math.Max(1, request.MinAliveCount.Value),
            OfflineSeconds = Math.Max(1, request.OfflineSeconds ?? existing?.OfflineSeconds ?? 60),
            DurationMinutes = Math.Max(0, request.DurationMinutes ?? existing?.DurationMinutes ?? 0),
            NotifyMinuteQueue = request.NotifyMinuteQueue.Count > 0
                ? MetricsAndAlertsUtilities.FormatNotifyMinuteQueue(request.NotifyMinuteQueue)
                : existing?.NotifyMinuteQueue ?? "0,5,10,120",
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }

    async Task UpsertOptionalCheckUrlAsync(UpsertMonitorTargetRequest request, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CheckUrl))
        {
            return;
        }

        if (!Uri.TryCreate(request.CheckUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("CheckUrl must be an absolute URL.", nameof(request));
        }

        var existing = await _repository.GetFirstTargetUrlMonitorRuleAsync(request.TargetKey.Trim(), request.TargetType, cancellationToken);
        await _repository.UpsertTargetUrlMonitorRuleAsync(new TargetUrlMonitorRule
        {
            Id = existing?.Id ?? 0,
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            UrlName = string.IsNullOrWhiteSpace(request.CheckUrlName) ? existing?.UrlName : request.CheckUrlName.Trim(),
            Url = request.CheckUrl.Trim(),
            HttpMethod = string.IsNullOrWhiteSpace(request.HttpMethod) ? existing?.HttpMethod ?? "GET" : request.HttpMethod.Trim().ToUpperInvariant(),
            TimeoutSeconds = Math.Max(1, request.CheckTimeoutSeconds ?? existing?.TimeoutSeconds ?? 10),
            CheckIntervalSeconds = Math.Max(1, request.CheckIntervalSeconds ?? existing?.CheckIntervalSeconds ?? 30),
            ExpectedStatusCodes = request.ExpectedStatusCodes.Count > 0
                ? MetricsAndAlertsUtilities.FormatExpectedStatusCodes(request.ExpectedStatusCodes)
                : existing?.ExpectedStatusCodes ?? "200",
            DurationMinutes = Math.Max(0, request.DurationMinutes ?? existing?.DurationMinutes ?? 0),
            NotifyMinuteQueue = request.NotifyMinuteQueue.Count > 0
                ? MetricsAndAlertsUtilities.FormatNotifyMinuteQueue(request.NotifyMinuteQueue)
                : existing?.NotifyMinuteQueue ?? "0,5,10,120",
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }
}

public sealed class TargetAliveAlertRuleService
{
    readonly IMetricsAndAlertsRepository _repository;

    public TargetAliveAlertRuleService(IMetricsAndAlertsRepository repository)
    {
        _repository = repository;
    }

    public async Task<TargetAliveAlertRuleDto> UpsertAsync(UpsertTargetAliveAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TargetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var rule = await _repository.UpsertTargetAliveAlertRuleAsync(new TargetAliveAlertRule
        {
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            MinAliveCount = Math.Max(1, request.MinAliveCount),
            OfflineSeconds = Math.Max(1, request.OfflineSeconds),
            DurationMinutes = Math.Max(0, request.DurationMinutes),
            NotifyMinuteQueue = MetricsAndAlertsUtilities.FormatNotifyMinuteQueue(request.NotifyMinuteQueue),
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return ToDto(rule);
    }

    public async Task<TargetAliveAlertRuleDto?> GetAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        var rule = await _repository.GetTargetAliveAlertRuleAsync(targetKey.Trim(), targetType, cancellationToken);
        return rule is null ? null : ToDto(rule);
    }

    static TargetAliveAlertRuleDto ToDto(TargetAliveAlertRule rule)
    {
        return new TargetAliveAlertRuleDto
        {
            Id = rule.Id,
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            MinAliveCount = rule.MinAliveCount,
            OfflineSeconds = rule.OfflineSeconds,
            DurationMinutes = rule.DurationMinutes,
            NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
            IsEnabled = rule.IsEnabled
        };
    }
}

public sealed class TargetUrlMonitorRuleService
{
    readonly IMetricsAndAlertsRepository _repository;

    public TargetUrlMonitorRuleService(IMetricsAndAlertsRepository repository)
    {
        _repository = repository;
    }

    public async Task<TargetUrlMonitorRuleDto> UpsertAsync(UpsertTargetUrlMonitorRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TargetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(request));
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Url must be an absolute URL.", nameof(request));
        }

        var method = string.IsNullOrWhiteSpace(request.HttpMethod) ? "GET" : request.HttpMethod.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;
        var rule = await _repository.UpsertTargetUrlMonitorRuleAsync(new TargetUrlMonitorRule
        {
            Id = request.Id,
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            UrlName = string.IsNullOrWhiteSpace(request.UrlName) ? null : request.UrlName.Trim(),
            Url = request.Url.Trim(),
            HttpMethod = method,
            TimeoutSeconds = Math.Max(1, request.TimeoutSeconds),
            CheckIntervalSeconds = Math.Max(1, request.CheckIntervalSeconds),
            ExpectedStatusCodes = MetricsAndAlertsUtilities.FormatExpectedStatusCodes(request.ExpectedStatusCodes),
            DurationMinutes = Math.Max(0, request.DurationMinutes),
            NotifyMinuteQueue = MetricsAndAlertsUtilities.FormatNotifyMinuteQueue(request.NotifyMinuteQueue),
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return ToDto(rule);
    }

    public async Task<IReadOnlyList<TargetUrlMonitorRuleDto>> ListAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        var rules = await _repository.ListTargetUrlMonitorRulesAsync(targetKey.Trim(), targetType, cancellationToken);
        return rules.Select(ToDto).ToArray();
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _repository.DisableTargetUrlMonitorRuleAsync(id, cancellationToken);
    }

    static TargetUrlMonitorRuleDto ToDto(TargetUrlMonitorRule rule)
    {
        return new TargetUrlMonitorRuleDto
        {
            Id = rule.Id,
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            UrlName = rule.UrlName,
            Url = rule.Url,
            HttpMethod = rule.HttpMethod,
            TimeoutSeconds = rule.TimeoutSeconds,
            CheckIntervalSeconds = rule.CheckIntervalSeconds,
            ExpectedStatusCodes = MetricsAndAlertsUtilities.ParseExpectedStatusCodes(rule.ExpectedStatusCodes).OrderBy(x => x).ToList(),
            DurationMinutes = rule.DurationMinutes,
            NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
            IsEnabled = rule.IsEnabled,
            LastCheckAt = rule.LastCheckAt
        };
    }
}

public sealed class AlertQueryService
{
    readonly IMetricsAndAlertsRepository _repository;

    public AlertQueryService(IMetricsAndAlertsRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<AlertStateDto>> ListAlertStatesAsync(AlertStateListQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.ListAlertStatesAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<AlertNotifyLogDto>> ListAlertNotifyLogsAsync(AlertNotifyLogListQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.ListAlertNotifyLogsAsync(query, cancellationToken);
    }
}

public sealed class AlertStateService
{
    readonly IMetricsAndAlertsRepository _repository;
    readonly IAlertNotifier _notifier;
    readonly ILogger<AlertStateService> _logger;

    public AlertStateService(
        IMetricsAndAlertsRepository repository,
        IAlertNotifier notifier,
        ILogger<AlertStateService> logger)
    {
        _repository = repository;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task HandleCheckResultAsync(AlertCheckResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var now = DateTime.UtcNow;
        var alertKey = MetricsAndAlertsUtilities.BuildAlertKey(result.AlertType, result.RuleId);
        var state = await _repository.GetAlertStateByKeyAsync(alertKey, cancellationToken)
            ?? new AlertState
            {
                AlertType = result.AlertType,
                RuleId = result.RuleId,
                TargetKey = result.TargetKey,
                TargetType = result.TargetType,
                AlertKey = alertKey
            };

        state.AlertType = result.AlertType;
        state.RuleId = result.RuleId;
        state.TargetKey = result.TargetKey;
        state.TargetType = result.TargetType;
        state.CurrentValue = result.CurrentValue;
        state.ThresholdValue = result.ThresholdValue;
        state.LastError = result.ErrorMessage;
        state.UpdatedAt = now;

        if (result.IsNormal)
        {
            await HandleNormalAsync(state, result, now, cancellationToken);
            return;
        }

        await HandleAbnormalAsync(state, result, now, cancellationToken);
    }

    async Task HandleNormalAsync(AlertState state, AlertCheckResult result, DateTime now, CancellationToken cancellationToken)
    {
        var wasFiring = state.IsActive && state.FiringAt.HasValue;
        if (wasFiring)
        {
            state.RecoveredAt = now;
            state = await _repository.SaveAlertStateAsync(state, cancellationToken);

            var message = await BuildMessageAsync(result, NotifyType.Recover, cancellationToken);
            var notifyResult = await _notifier.SendRecoverAsync(message, cancellationToken);
            await AddNotifyLogAsync(state, message, null, notifyResult, cancellationToken);
        }

        state.IsActive = false;
        state.FirstAbnormalAt = null;
        state.FiringAt = null;
        state.NextNotifyIndex = 0;
        state.LastNotifyAt = null;
        state.LastError = null;
        state.UpdatedAt = now;
        await _repository.SaveAlertStateAsync(state, cancellationToken);
    }

    async Task HandleAbnormalAsync(AlertState state, AlertCheckResult result, DateTime now, CancellationToken cancellationToken)
    {
        state.FirstAbnormalAt ??= now;
        state.RecoveredAt = null;

        if (!state.IsActive)
        {
            var duration = TimeSpan.FromMinutes(Math.Max(0, result.DurationMinutes));
            if (now - state.FirstAbnormalAt.Value >= duration)
            {
                state.IsActive = true;
                state.FiringAt = now;
                state.NextNotifyIndex = 0;
            }
        }

        state = await _repository.SaveAlertStateAsync(state, cancellationToken);

        if (!state.IsActive || !state.FiringAt.HasValue)
        {
            return;
        }

        var queue = result.NotifyMinuteQueue.Count > 0
            ? result.NotifyMinuteQueue.Where(x => x >= 0).Distinct().OrderBy(x => x).ToList()
            : new List<int> { 0 };

        if (state.NextNotifyIndex >= queue.Count)
        {
            return;
        }

        var dueMinute = queue[state.NextNotifyIndex];
        var firingMinutes = (now - state.FiringAt.Value).TotalMinutes;
        if (firingMinutes + 0.0001 < dueMinute)
        {
            return;
        }

        var message = await BuildMessageAsync(result, NotifyType.Alert, cancellationToken);
        var notifyResult = await _notifier.SendAlertAsync(message, cancellationToken);
        await AddNotifyLogAsync(state, message, dueMinute, notifyResult, cancellationToken);

        state.NextNotifyIndex++;
        state.LastNotifyAt = now;
        state.UpdatedAt = now;
        await _repository.SaveAlertStateAsync(state, cancellationToken);
    }

    async Task<AlertMessage> BuildMessageAsync(AlertCheckResult result, NotifyType notifyType, CancellationToken cancellationToken)
    {
        var target = await _repository.GetMonitorTargetAsync(result.TargetKey, result.TargetType, cancellationToken);
        var targetName = MetricsAndAlertsUtilities.GetDisplayName(result.TargetKey, target?.TargetName);
        var targetTypeName = MetricsAndAlertsUtilities.GetTargetTypeName(result.TargetType);
        var isRecover = notifyType == NotifyType.Recover;
        var title = BuildTitle(result, targetName, isRecover);
        var content = BuildContent(result, targetName, targetTypeName, isRecover);

        return new AlertMessage
        {
            AlertType = result.AlertType,
            NotifyType = notifyType,
            ToUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(target?.NotifyUsers),
            TargetKey = result.TargetKey,
            TargetName = targetName,
            TargetType = result.TargetType,
            Title = title,
            Content = content,
            CurrentValue = result.CurrentValue,
            ThresholdValue = result.ThresholdValue,
            ErrorMessage = result.ErrorMessage
        };
    }

    string BuildTitle(AlertCheckResult result, string targetName, bool isRecover)
    {
        if (result.AlertType == AlertType.TargetUrlMonitorFailed)
        {
            var urlName = string.IsNullOrWhiteSpace(result.UrlName) ? result.Url : result.UrlName;
            return isRecover
                ? $"【恢复】URL监控已恢复：{urlName}"
                : $"【告警】URL监控失败：{urlName}";
        }

        if (result.TargetType == MonitorTargetType.Server)
        {
            return isRecover
                ? $"【恢复】服务器已恢复：{targetName}"
                : $"【告警】服务器离线：{targetName}";
        }

        return isRecover
            ? $"【恢复】应用在线实例数量已恢复：{targetName}"
            : $"【告警】应用在线实例数量不足：{targetName}";
    }

    static string BuildContent(AlertCheckResult result, string targetName, string targetTypeName, bool isRecover)
    {
        var lines = new List<string>
        {
            $"目标类型：{targetTypeName}",
            $"目标名称：{targetName}",
            $"目标标识：{result.TargetKey}"
        };

        if (result.AlertType == AlertType.TargetUrlMonitorFailed)
        {
            lines.Add($"URL名称：{(string.IsNullOrWhiteSpace(result.UrlName) ? result.Url : result.UrlName)}");
            lines.Add($"URL：{result.Url}");
            lines.Add($"错误信息：{(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "-" : result.ErrorMessage)}");
            lines.Add($"持续异常：{result.DurationMinutes} 分钟");
        }
        else
        {
            lines.Add($"当前在线数量：{result.CurrentValue}");
            lines.Add($"最小要求数量：{result.ThresholdValue}");
            if (result.OfflineSeconds.HasValue)
            {
                lines.Add($"心跳超时时间：{result.OfflineSeconds.Value} 秒");
            }

            if (!isRecover)
            {
                lines.Add($"持续异常：{result.DurationMinutes} 分钟");
                lines.Add(result.TargetType == MonitorTargetType.Server
                    ? "异常原因：服务器心跳离线"
                    : "异常原因：在线实例数量低于最小要求");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    async Task AddNotifyLogAsync(
        AlertState state,
        AlertMessage message,
        int? notifyMinute,
        NotifyResult notifyResult,
        CancellationToken cancellationToken)
    {
        if (!notifyResult.IsSuccess)
        {
            _logger.LogWarning(
                "Alert notification failed. AlertKey={AlertKey}, Error={Error}",
                state.AlertKey,
                notifyResult.ErrorMessage);
        }

        await _repository.AddAlertNotifyLogAsync(new AlertNotifyLog
        {
            AlertStateId = state.Id,
            AlertType = state.AlertType,
            NotifyType = message.NotifyType,
            NotifyMinute = notifyMinute,
            TargetKey = state.TargetKey,
            TargetType = state.TargetType,
            Title = message.Title,
            Content = message.Content,
            IsSuccess = notifyResult.IsSuccess,
            ErrorMessage = notifyResult.ErrorMessage,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}

public sealed class TargetAliveAlertWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<AlertOptions> _options;
    readonly ILogger<TargetAliveAlertWorker> _logger;

    public TargetAliveAlertWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AlertOptions> options,
        ILogger<TargetAliveAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.TargetAliveWorkerFailed(ex);
            }

            var seconds = Math.Max(1, _options.CurrentValue.TargetAliveWorkerIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricsAndAlertsRepository>();
        var prometheus = scope.ServiceProvider.GetRequiredService<IPrometheusQueryClient>();
        var alertState = scope.ServiceProvider.GetRequiredService<AlertStateService>();

        var rules = await repository.ListEnabledTargetAliveAlertRulesAsync(cancellationToken);
        foreach (var rule in rules)
        {
            AlertCheckResult result;
            try
            {
                var onlineCount = await prometheus.GetOnlineCountAsync(rule.TargetKey, rule.TargetType, rule.OfflineSeconds, cancellationToken);
                result = new AlertCheckResult
                {
                    AlertType = AlertType.TargetAliveCountLow,
                    RuleId = rule.Id,
                    TargetKey = rule.TargetKey,
                    TargetType = rule.TargetType,
                    IsNormal = onlineCount >= rule.MinAliveCount,
                    DurationMinutes = rule.DurationMinutes,
                    NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
                    CurrentValue = onlineCount.ToString(),
                    ThresholdValue = rule.MinAliveCount.ToString(),
                    OfflineSeconds = rule.OfflineSeconds
                };
            }
            catch (Exception ex)
            {
                result = new AlertCheckResult
                {
                    AlertType = AlertType.TargetAliveCountLow,
                    RuleId = rule.Id,
                    TargetKey = rule.TargetKey,
                    TargetType = rule.TargetType,
                    IsNormal = false,
                    DurationMinutes = rule.DurationMinutes,
                    NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
                    CurrentValue = "query_error",
                    ThresholdValue = rule.MinAliveCount.ToString(),
                    ErrorMessage = ex.Message,
                    OfflineSeconds = rule.OfflineSeconds
                };
            }

            await alertState.HandleCheckResultAsync(result, cancellationToken);
        }
    }
}

public sealed class TargetUrlMonitorWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<AlertOptions> _options;
    readonly ILogger<TargetUrlMonitorWorker> _logger;

    public TargetUrlMonitorWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AlertOptions> options,
        ILogger<TargetUrlMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.UrlMonitorWorkerFailed(ex);
            }

            var seconds = Math.Max(1, _options.CurrentValue.UrlMonitorWorkerScanIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricsAndAlertsRepository>();
        var client = scope.ServiceProvider.GetRequiredService<IUrlMonitorClient>();
        var recorder = scope.ServiceProvider.GetRequiredService<IUrlMonitorMetricRecorder>();
        var alertState = scope.ServiceProvider.GetRequiredService<AlertStateService>();
        var now = DateTime.UtcNow;
        var rules = await repository.ListDueTargetUrlMonitorRulesAsync(now, cancellationToken);

        foreach (var rule in rules)
        {
            var check = await client.CheckAsync(rule, cancellationToken);
            var checkedAt = DateTime.UtcNow;
            recorder.Record(rule, check, checkedAt);
            await repository.UpdateTargetUrlMonitorLastCheckAsync(rule.Id, checkedAt, cancellationToken);

            await alertState.HandleCheckResultAsync(new AlertCheckResult
            {
                AlertType = AlertType.TargetUrlMonitorFailed,
                RuleId = rule.Id,
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                IsNormal = check.IsSuccess,
                DurationMinutes = rule.DurationMinutes,
                NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
                CurrentValue = check.StatusCode?.ToString() ?? "request_error",
                ThresholdValue = rule.ExpectedStatusCodes,
                ErrorMessage = check.ErrorMessage,
                UrlName = rule.UrlName,
                Url = rule.Url
            }, cancellationToken);
        }
    }
}

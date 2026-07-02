using MetricsAndAlertsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;

namespace Infrastructure.Sql.MetricsAndAlerts;

public sealed class MetricsAndAlertsRepository : IMetricsAndAlertsRepository, IMetricRulesRepository
{
    readonly MetricsAndAlertsDbContext _db;

    public MetricsAndAlertsRepository(MetricsAndAlertsDbContext db)
    {
        _db = db;
    }

    public async Task<MonitorTargetGroup> UpsertGroupAsync(MonitorTargetGroup group, CancellationToken cancellationToken = default)
    {
        var entity = group.Id > 0
            ? await _db.MonitorTargetGroups.AsTracking().FirstOrDefaultAsync(x => x.Id == group.Id, cancellationToken)
            : await _db.MonitorTargetGroups.AsTracking().FirstOrDefaultAsync(x => x.GroupName == group.GroupName, cancellationToken);

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new MonitorTargetGroup
            {
                GroupName = group.GroupName,
                SortOrder = group.SortOrder,
                IsEnabled = group.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.MonitorTargetGroups.Add(entity);
        }
        else
        {
            entity.GroupName = group.GroupName;
            entity.SortOrder = group.SortOrder;
            entity.IsEnabled = group.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<MonitorTargetGroup>> ListGroupsAsync(bool includeDisabled = false, CancellationToken cancellationToken = default)
    {
        var query = _db.MonitorTargetGroups.AsQueryable();
        if (!includeDisabled)
        {
            query = query.Where(x => x.IsEnabled);
        }

        return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToArrayAsync(cancellationToken);
    }

    public async Task<bool> DisableGroupAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MonitorTargetGroups.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> GroupExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return _db.MonitorTargetGroups.AnyAsync(x => x.Id == id && x.IsEnabled, cancellationToken);
    }

    public async Task<MonitorTarget> UpsertMonitorTargetAsync(MonitorTarget target, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MonitorTargets.AsTracking().FirstOrDefaultAsync(
            x => x.TargetKey == target.TargetKey && x.TargetType == target.TargetType,
            cancellationToken);

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new MonitorTarget
            {
                GroupId = target.GroupId,
                TargetKey = target.TargetKey,
                TargetName = target.TargetName,
                TargetType = target.TargetType,
                SortOrder = target.SortOrder,
                NotifyUsers = target.NotifyUsers,
                IsEnabled = target.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.MonitorTargets.Add(entity);
        }
        else
        {
            entity.GroupId = target.GroupId;
            entity.TargetName = target.TargetName;
            entity.SortOrder = target.SortOrder;
            entity.NotifyUsers = target.NotifyUsers;
            entity.IsEnabled = target.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<MonitorTarget?> GetMonitorTargetAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        return _db.MonitorTargets.FirstOrDefaultAsync(x => x.TargetKey == targetKey && x.TargetType == targetType, cancellationToken);
    }

    public async Task<IReadOnlyList<MonitorTargetListItemDto>> ListMonitorTargetsAsync(MonitorTargetListQuery query, CancellationToken cancellationToken = default)
    {
        var targetQuery = _db.MonitorTargets.AsQueryable();
        if (query.TargetType.HasValue)
        {
            targetQuery = targetQuery.Where(x => x.TargetType == query.TargetType.Value);
        }

        if (query.GroupId.HasValue)
        {
            targetQuery = targetQuery.Where(x => x.GroupId == query.GroupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetKeyEquals))
        {
            targetQuery = targetQuery.Where(x => x.TargetKey == query.TargetKeyEquals);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            targetQuery = targetQuery.Where(x => x.TargetKey.Contains(keyword) || (x.TargetName != null && x.TargetName.Contains(keyword)));
        }

        var targets = await targetQuery
            .OrderBy(x => x.GroupId ?? long.MaxValue)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.TargetType)
            .ThenBy(x => x.TargetKey)
            .ToArrayAsync(cancellationToken);
        var groups = await _db.MonitorTargetGroups.ToDictionaryAsync(x => x.Id, cancellationToken);
        return targets.Select(target =>
        {
            return new MonitorTargetListItemDto
            {
                Id = target.Id,
                GroupId = target.GroupId,
                GroupName = target.GroupId.HasValue && groups.TryGetValue(target.GroupId.Value, out var group) ? group.GroupName : null,
                TargetKey = target.TargetKey,
                TargetName = target.TargetName,
                TargetType = target.TargetType,
                SortOrder = target.SortOrder,
                NotifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(target.NotifyUsers),
                IsEnabled = target.IsEnabled
            };
        }).ToArray();
    }

    public async Task<TargetAliveAlertRule> UpsertTargetAliveAlertRuleAsync(TargetAliveAlertRule rule, CancellationToken cancellationToken = default)
    {
        TargetAliveAlertRule? entity = null;
        if (rule.Id > 0)
        {
            entity = await _db.TargetAliveAlertRules.AsTracking().FirstOrDefaultAsync(x => x.Id == rule.Id, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new TargetAliveAlertRule
            {
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                RuleName = rule.RuleName,
                MinAliveCount = rule.MinAliveCount,
                OfflineSeconds = rule.OfflineSeconds,
                DurationMinutes = rule.DurationMinutes,
                NotifyMinuteQueue = rule.NotifyMinuteQueue,
                NotifyUsers = rule.NotifyUsers,
                IsEnabled = rule.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.TargetAliveAlertRules.Add(entity);
        }
        else
        {
            entity.TargetKey = rule.TargetKey;
            entity.TargetType = rule.TargetType;
            entity.RuleName = rule.RuleName;
            entity.MinAliveCount = rule.MinAliveCount;
            entity.OfflineSeconds = rule.OfflineSeconds;
            entity.DurationMinutes = rule.DurationMinutes;
            entity.NotifyMinuteQueue = rule.NotifyMinuteQueue;
            entity.NotifyUsers = rule.NotifyUsers;
            entity.IsEnabled = rule.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<TargetAliveAlertRule?> GetTargetAliveAlertRuleAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        return _db.TargetAliveAlertRules
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.TargetKey == targetKey && x.TargetType == targetType, cancellationToken);
    }

    public async Task<IReadOnlyList<TargetAliveAlertRule>> ListTargetAliveAlertRulesAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        return await _db.TargetAliveAlertRules
            .Where(x => x.TargetKey == targetKey && x.TargetType == targetType)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TargetAliveAlertRule>> ListEnabledTargetAliveAlertRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.TargetAliveAlertRules.Where(x => x.IsEnabled).ToArrayAsync(cancellationToken);
    }

    public async Task<bool> DisableTargetAliveAlertRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TargetAliveAlertRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TargetUrlMonitorRule> UpsertTargetUrlMonitorRuleAsync(TargetUrlMonitorRule rule, CancellationToken cancellationToken = default)
    {
        TargetUrlMonitorRule? entity = null;
        if (rule.Id > 0)
        {
            entity = await _db.TargetUrlMonitorRules.AsTracking().FirstOrDefaultAsync(x => x.Id == rule.Id, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new TargetUrlMonitorRule
            {
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                RuleName = rule.RuleName,
                UrlName = rule.UrlName,
                Url = rule.Url,
                HttpMethod = rule.HttpMethod,
                TimeoutSeconds = rule.TimeoutSeconds,
                CheckIntervalSeconds = rule.CheckIntervalSeconds,
                ExpectedStatusCodes = rule.ExpectedStatusCodes,
                DurationMinutes = rule.DurationMinutes,
                NotifyMinuteQueue = rule.NotifyMinuteQueue,
                NotifyUsers = rule.NotifyUsers,
                IsEnabled = rule.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.TargetUrlMonitorRules.Add(entity);
        }
        else
        {
            entity.TargetKey = rule.TargetKey;
            entity.TargetType = rule.TargetType;
            entity.RuleName = rule.RuleName;
            entity.UrlName = rule.UrlName;
            entity.Url = rule.Url;
            entity.HttpMethod = rule.HttpMethod;
            entity.TimeoutSeconds = rule.TimeoutSeconds;
            entity.CheckIntervalSeconds = rule.CheckIntervalSeconds;
            entity.ExpectedStatusCodes = rule.ExpectedStatusCodes;
            entity.DurationMinutes = rule.DurationMinutes;
            entity.NotifyMinuteQueue = rule.NotifyMinuteQueue;
            entity.NotifyUsers = rule.NotifyUsers;
            entity.IsEnabled = rule.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<TargetUrlMonitorRule>> ListTargetUrlMonitorRulesAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        return await _db.TargetUrlMonitorRules
            .Where(x => x.TargetKey == targetKey && x.TargetType == targetType)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task<TargetUrlMonitorRule?> GetFirstTargetUrlMonitorRuleAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default)
    {
        return _db.TargetUrlMonitorRules
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.TargetKey == targetKey && x.TargetType == targetType && x.IsEnabled, cancellationToken);
    }

    public async Task<IReadOnlyList<TargetUrlMonitorRule>> ListDueTargetUrlMonitorRulesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var rules = await _db.TargetUrlMonitorRules.Where(x => x.IsEnabled).ToArrayAsync(cancellationToken);
        return rules
            .Where(x => !x.LastCheckAt.HasValue || x.LastCheckAt.Value.AddSeconds(Math.Max(1, x.CheckIntervalSeconds)) <= nowUtc)
            .ToArray();
    }

    public async Task<bool> DisableTargetUrlMonitorRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TargetUrlMonitorRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateTargetUrlMonitorLastCheckAsync(long id, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TargetUrlMonitorRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastCheckAt = nowUtc;
        entity.UpdatedAt = nowUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<AlertState?> GetAlertStateByKeyAsync(string alertKey, CancellationToken cancellationToken = default)
    {
        return _db.AlertStates.FirstOrDefaultAsync(x => x.AlertKey == alertKey, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertState>> ListAlertStatesByRuleAsync(AlertType alertType, long ruleId, CancellationToken cancellationToken = default)
    {
        return await _db.AlertStates
            .Where(x => x.AlertType == alertType && x.RuleId == ruleId)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AlertState> SaveAlertStateAsync(AlertState state, CancellationToken cancellationToken = default)
    {
        var isInsert = state.Id == 0;
        if (isInsert)
        {
            _db.AlertStates.Add(state);
        }
        else
        {
            _db.AlertStates.Update(state);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return state;
        }
        catch (DbUpdateException ex) when (isInsert && IsAlertKeyConflict(ex))
        {
            _db.Entry(state).State = EntityState.Detached;
            var existing = await _db.AlertStates.AsTracking()
                .FirstOrDefaultAsync(x => x.AlertKey == state.AlertKey, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            existing.AlertType = state.AlertType;
            existing.RuleId = state.RuleId;
            existing.TargetKey = state.TargetKey;
            existing.TargetType = state.TargetType;
            existing.IsActive = state.IsActive;
            existing.FirstAbnormalAt = state.FirstAbnormalAt;
            existing.FiringAt = state.FiringAt;
            existing.RecoveredAt = state.RecoveredAt;
            existing.LastNotifyAt = state.LastNotifyAt;
            existing.NextNotifyIndex = state.NextNotifyIndex;
            existing.CurrentValue = state.CurrentValue;
            existing.ThresholdValue = state.ThresholdValue;
            existing.LastError = state.LastError;
            existing.UpdatedAt = state.UpdatedAt;

            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }
    }

    public async Task DeleteAlertStatesAsync(IReadOnlyCollection<long> stateIds, CancellationToken cancellationToken = default)
    {
        if (stateIds.Count == 0)
        {
            return;
        }

        var ids = stateIds.Distinct().ToArray();
        var logs = await _db.AlertNotifyLogs
            .Where(x => ids.Contains(x.AlertStateId))
            .ToArrayAsync(cancellationToken);
        if (logs.Length > 0)
        {
            _db.AlertNotifyLogs.RemoveRange(logs);
        }

        var states = await _db.AlertStates
            .Where(x => ids.Contains(x.Id))
            .ToArrayAsync(cancellationToken);
        if (states.Length > 0)
        {
            _db.AlertStates.RemoveRange(states);
        }

        if (logs.Length == 0 && states.Length == 0)
        {
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    static bool IsAlertKeyConflict(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UX_AlertState_AlertKey", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("2627", StringComparison.OrdinalIgnoreCase)
            || message.Contains("2601", StringComparison.OrdinalIgnoreCase);
    }

    public async Task AddAlertNotifyLogAsync(AlertNotifyLog log, CancellationToken cancellationToken = default)
    {
        _db.AlertNotifyLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertStateDto>> ListAlertStatesAsync(AlertStateListQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _db.AlertStates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.TargetKey))
        {
            dbQuery = dbQuery.Where(x => x.TargetKey == query.TargetKey);
        }

        if (query.TargetType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.TargetType == query.TargetType.Value);
        }

        if (query.AlertType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.AlertType == query.AlertType.Value);
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.IsActive == query.IsActive.Value);
        }

        return await dbQuery
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new AlertStateDto
            {
                Id = x.Id,
                AlertType = x.AlertType,
                RuleId = x.RuleId,
                TargetKey = x.TargetKey,
                TargetType = x.TargetType,
                AlertKey = x.AlertKey,
                IsActive = x.IsActive,
                FirstAbnormalAt = x.FirstAbnormalAt,
                FiringAt = x.FiringAt,
                RecoveredAt = x.RecoveredAt,
                LastNotifyAt = x.LastNotifyAt,
                NextNotifyIndex = x.NextNotifyIndex,
                CurrentValue = x.CurrentValue,
                ThresholdValue = x.ThresholdValue,
                LastError = x.LastError,
                UpdatedAt = x.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertNotifyLogDto>> ListAlertNotifyLogsAsync(AlertNotifyLogListQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _db.AlertNotifyLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.TargetKey))
        {
            dbQuery = dbQuery.Where(x => x.TargetKey == query.TargetKey);
        }

        if (query.TargetType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.TargetType == query.TargetType.Value);
        }

        if (query.AlertType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.AlertType == query.AlertType.Value);
        }

        if (query.StartTime.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.CreatedAt >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.CreatedAt <= query.EndTime.Value);
        }

        return await dbQuery
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AlertNotifyLogDto
            {
                Id = x.Id,
                AlertStateId = x.AlertStateId,
                AlertType = x.AlertType,
                NotifyType = x.NotifyType,
                NotifyMinute = x.NotifyMinute,
                TargetKey = x.TargetKey,
                TargetType = x.TargetType,
                Title = x.Title,
                Content = x.Content,
                IsSuccess = x.IsSuccess,
                ErrorMessage = x.ErrorMessage,
                CreatedAt = x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MetricAlertRule> UpsertMetricAlertRuleAsync(MetricAlertRule rule, CancellationToken cancellationToken = default)
    {
        MetricAlertRule? entity = null;
        if (rule.Id > 0)
        {
            entity = await _db.MetricAlertRules.AsTracking().FirstOrDefaultAsync(x => x.Id == rule.Id, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new MetricAlertRule
            {
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                RuleName = rule.RuleName,
                QueryItemsJson = rule.QueryItemsJson,
                Formula = rule.Formula,
                DurationMinutes = rule.DurationMinutes,
                NotifyMinuteQueue = rule.NotifyMinuteQueue,
                NotifyUsers = rule.NotifyUsers,
                MessageTemplate = rule.MessageTemplate,
                RecoverTemplate = rule.RecoverTemplate,
                LastValuesJson = rule.LastValuesJson,
                AggregateInstances = rule.AggregateInstances,
                IsEnabled = rule.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.MetricAlertRules.Add(entity);
        }
        else
        {
            var queryItemsChanged = !string.Equals(entity.QueryItemsJson, rule.QueryItemsJson, StringComparison.Ordinal);
            entity.TargetKey = rule.TargetKey;
            entity.TargetType = rule.TargetType;
            entity.RuleName = rule.RuleName;
            entity.QueryItemsJson = rule.QueryItemsJson;
            entity.Formula = rule.Formula;
            entity.DurationMinutes = rule.DurationMinutes;
            entity.NotifyMinuteQueue = rule.NotifyMinuteQueue;
            entity.NotifyUsers = rule.NotifyUsers;
            entity.MessageTemplate = rule.MessageTemplate;
            entity.RecoverTemplate = rule.RecoverTemplate;
            entity.AggregateInstances = rule.AggregateInstances;
            if (queryItemsChanged)
            {
                entity.LastValuesJson = null;
            }
            entity.IsEnabled = rule.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<MetricAlertRule>> ListMetricAlertRulesAsync(MetricAlertRuleListQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _db.MetricAlertRules.AsQueryable();
        if (!query.IncludeDisabled)
        {
            dbQuery = dbQuery.Where(x => x.IsEnabled);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetKey))
        {
            dbQuery = dbQuery.Where(x => x.TargetKey == query.TargetKey);
        }

        if (query.TargetType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.TargetType == query.TargetType.Value);
        }

        return await dbQuery
            .OrderBy(x => x.TargetKey)
            .ThenBy(x => x.RuleName)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MetricAlertRule>> ListEnabledMetricAlertRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.MetricAlertRules
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> DisableMetricAlertRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MetricAlertRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateMetricAlertRuleLastValuesAsync(long id, string lastValuesJson, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MetricAlertRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastValuesJson = lastValuesJson;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScheduledMetricMessageRule> UpsertScheduledMetricMessageRuleAsync(ScheduledMetricMessageRule rule, CancellationToken cancellationToken = default)
    {
        ScheduledMetricMessageRule? entity = null;
        if (rule.Id > 0)
        {
            entity = await _db.ScheduledMetricMessageRules.AsTracking().FirstOrDefaultAsync(x => x.Id == rule.Id, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new ScheduledMetricMessageRule
            {
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                RuleName = rule.RuleName,
                QueryItemsJson = rule.QueryItemsJson,
                MessageTemplate = rule.MessageTemplate,
                Cron = rule.Cron,
                TimeZoneId = rule.TimeZoneId,
                NextRunAt = rule.NextRunAt,
                LastRunAt = rule.LastRunAt,
                NotifyUsers = rule.NotifyUsers,
                AggregateInstances = rule.AggregateInstances,
                IsEnabled = rule.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ScheduledMetricMessageRules.Add(entity);
        }
        else
        {
            entity.TargetKey = rule.TargetKey;
            entity.TargetType = rule.TargetType;
            entity.RuleName = rule.RuleName;
            entity.QueryItemsJson = rule.QueryItemsJson;
            entity.MessageTemplate = rule.MessageTemplate;
            entity.Cron = rule.Cron;
            entity.TimeZoneId = rule.TimeZoneId;
            entity.NextRunAt = rule.NextRunAt;
            entity.NotifyUsers = rule.NotifyUsers;
            entity.AggregateInstances = rule.AggregateInstances;
            entity.IsEnabled = rule.IsEnabled;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<ScheduledMetricMessageRule>> ListScheduledMetricMessageRulesAsync(ScheduledMetricMessageRuleListQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _db.ScheduledMetricMessageRules.AsQueryable();
        if (!query.IncludeDisabled)
        {
            dbQuery = dbQuery.Where(x => x.IsEnabled);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetKey))
        {
            dbQuery = dbQuery.Where(x => x.TargetKey == query.TargetKey);
        }

        if (query.TargetType.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.TargetType == query.TargetType.Value);
        }

        return await dbQuery
            .OrderBy(x => x.TargetKey)
            .ThenBy(x => x.RuleName)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledMetricMessageRule>> ListDueScheduledMetricMessageRulesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await _db.ScheduledMetricMessageRules
            .Where(x => x.IsEnabled && (!x.NextRunAt.HasValue || x.NextRunAt <= nowUtc))
            .OrderBy(x => x.NextRunAt)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> DisableScheduledMetricMessageRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ScheduledMetricMessageRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateScheduledMetricMessageRuleRunAsync(long id, DateTime lastRunAtUtc, DateTime? nextRunAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ScheduledMetricMessageRules.AsTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastRunAt = lastRunAtUtc;
        entity.NextRunAt = nextRunAtUtc;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddScheduledMessageSendLogAsync(ScheduledMessageSendLog log, CancellationToken cancellationToken = default)
    {
        _db.ScheduledMessageSendLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

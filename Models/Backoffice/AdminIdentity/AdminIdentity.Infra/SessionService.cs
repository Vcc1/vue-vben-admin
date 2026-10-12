using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminIdentity.Infra;

/// <summary>
/// 会话（sid）与设备信任的权威实现。数据落 <c>Identity_Sessions</c> / <c>Identity_TrustedDevices</c>；
/// 每请求的等级判定优先走 <see cref="ISessionStateCache"/>（Redis），未命中或缓存故障时直查数据库。
/// </summary>
internal sealed class SessionService(
    AdminDbContext dbContext,
    IOptions<AdminIdentityOptions> options,
    ICurrentActorAccessor actorAccessor,
    ISecurityEventLogger securityEvents,
    IBackChannelLogoutNotifier logoutNotifier,
    ISessionStateCache stateCache,
    TimeProvider timeProvider,
    ILogger<SessionService> logger)
    : ISessionService, IDeviceTrustService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<string> CreateAsync(CreateSessionContext context, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lifetime = context.Lifetime > TimeSpan.Zero
            ? context.Lifetime
            : TimeSpan.FromDays(Math.Clamp(options.Value.Session.LifetimeDays, 1, 365));

        var session = new AdminSession
        {
            Sid = NewSid(),
            UserId = context.UserId,
            DeviceId = Normalize(context.DeviceId),
            DeviceInfoJson = context.DeviceInfoJson,
            LoginMethod = context.LoginMethod,
            PasswordVerifiedAtUtc = context.PasswordVerifiedAtUtc,
            StrongAuthAtUtc = context.StrongAuthAtUtc,
            StrongAuthMethod = context.StrongAuthMethod,
            ClientIdsJson = null,
            CreatedByIp = context.IpAddress,
            UserAgent = Truncate(context.UserAgent, 512),
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 预热缓存：登录后第一个请求就会走等级判定，省掉一次数据库往返。
        var state = ToState(session);
        await stateCache.SetAsync(state, CacheTtl(state, now), cancellationToken);

        return session.Sid;
    }

    public async Task<SessionState?> GetStateAsync(string? sid, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return null;
        }

        return await dbContext.Sessions.AsNoTracking()
            .Where(x => x.Sid == normalized)
            .Select(x => new SessionState(
                x.Sid, x.UserId, x.DeviceId, x.LoginMethod, x.PasswordVerifiedAtUtc, x.StrongAuthAtUtc,
                x.StrongAuthMethod, x.LastActivityAtUtc, x.ExpiresAtUtc, x.RevokedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CurrentSessionInfo?> GetCurrentAsync(string? sid, CancellationToken cancellationToken)
    {
        // 复用热路径：顺带完成粘性降级与活跃时间更新，保证返回的等级就是本次请求生效的等级。
        var normalized = Normalize(sid);
        var level = await ValidateAndTouchAsync(normalized, cancellationToken);
        if (level is null)
        {
            return null;
        }

        var session = await dbContext.Sessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null)
        {
            return null;
        }

        return new CurrentSessionInfo(
            session.Sid,
            level.Value,
            SessionLevels.ToAcr(level.Value),
            session.LoginMethod,
            session.DeviceId,
            DeviceInfoSummary.Parse(session.DeviceInfoJson),
            session.PasswordVerifiedAtUtc,
            session.StrongAuthAtUtc,
            session.StrongAuthMethod,
            ParseClients(session.ClientIdsJson),
            session.CreatedAtUtc,
            session.LastActivityAtUtc,
            session.ExpiresAtUtc);
    }

    public async Task<bool> IsRevokedAsync(string? sid, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await dbContext.Sessions.AsNoTracking()
            .AnyAsync(x => x.Sid == normalized && (x.RevokedAtUtc != null || x.ExpiresAtUtc <= now), cancellationToken);
    }

    public async Task<int> GetLevelAsync(string? sid, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(sid, cancellationToken);
        if (state is null || state.RevokedAtUtc is not null || state.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return 0;
        }

        return EvaluateLevel(state);
    }

    public async Task<int?> ValidateAndTouchAsync(string? sid, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return null;
        }

        // 吊销标记先查：它只写不删，即使事实缓存被并发请求回填过也拦得住，秒踢因此不依赖缓存新鲜度。
        if (await stateCache.IsRevokedAsync(normalized, cancellationToken))
        {
            return null;
        }

        // 热路径：命中缓存就完全不碰数据库；未命中（含 Redis 故障）退回权威表。
        var cached = await stateCache.TryGetAsync(normalized, cancellationToken);
        var state = cached ?? await GetStateAsync(normalized, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (state.RevokedAtUtc is not null || state.ExpiresAtUtc <= now)
        {
            return null;
        }

        var gap = now - state.LastActivityAtUtc;
        var passwordGap = PasswordGapWindow;
        var throttle = TimeSpan.FromSeconds(Math.Max(1, options.Value.Session.ActivityWriteThrottleSeconds));

        // 掉窗粘性：间隔超过 L2 窗口就清除密码核验事实，且不会因为后续活动自动恢复。
        var droppedLevel = gap > passwordGap && state.PasswordVerifiedAtUtc is not null;
        if (droppedLevel || gap > throttle)
        {
            state = state with
            {
                PasswordVerifiedAtUtc = droppedLevel ? null : state.PasswordVerifiedAtUtc,
                LastActivityAtUtc = now,
            };
            await WriteActivityAsync(normalized, state, cancellationToken);

            if (droppedLevel)
            {
                logger.LogInformation("会话 {Sid} 活跃间隔 {GapMinutes} 分钟超过 L2 窗口，已降级为 L1。",
                    normalized, (int)gap.TotalMinutes);
            }
        }

        if (cached is null || droppedLevel || gap > throttle)
        {
            await stateCache.SetAsync(state, CacheTtl(state, now), cancellationToken);
        }

        return SessionLevels.Evaluate(
            now, state.PasswordVerifiedAtUtc, state.StrongAuthAtUtc, state.LastActivityAtUtc,
            passwordGap, StrongAuthWindow);
    }

    public async Task TouchAsync(string? sid, CancellationToken cancellationToken)
    {
        await ValidateAndTouchAsync(sid, cancellationToken);
    }

    public async Task RevokeAsync(string sid, string reason, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return;
        }

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var actor = actorAccessor.Current;
        session.RevokedAtUtc = now;
        session.RevokedReason = Truncate(reason, 200);
        session.RevokedByActorId = actor.ActorId;
        session.RevokedByActorName = actor.ActorName;

        await RevokeRefreshTokensAsync(normalized, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 先写吊销标记、再清事实缓存：顺序反了会留一个窗口，让并发的读请求把"未吊销"的事实回填进来。
        await stateCache.MarkRevokedAsync(normalized, session.ExpiresAtUtc - now, cancellationToken);
        await stateCache.RemoveAsync(normalized, cancellationToken);

        await logoutNotifier.NotifyAsync(normalized, session.UserId, ParseClients(session.ClientIdsJson), cancellationToken);
        await securityEvents.LogAsync(new SecurityEvent(
            "SessionRevoked",
            "Medium",
            $"登录凭证已吊销（{reason}）。",
            new { Sid = normalized, session.UserId, session.DeviceId, Reason = reason, ActorId = actor.ActorId },
            ActorId: session.UserId.ToString()),
            cancellationToken);
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var sids = await dbContext.Sessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .Select(x => x.Sid)
            .ToListAsync(cancellationToken);

        foreach (var sid in sids)
        {
            await RevokeAsync(sid, reason, cancellationToken);
        }
        return sids.Count;
    }

    public async Task<int> RevokeAllForDeviceAsync(Guid userId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        var normalizedDevice = Normalize(deviceId);
        if (normalizedDevice is null)
        {
            return 0;
        }

        var sids = await dbContext.Sessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.DeviceId == normalizedDevice && x.RevokedAtUtc == null)
            .Select(x => x.Sid)
            .ToListAsync(cancellationToken);

        foreach (var sid in sids)
        {
            await RevokeAsync(sid, reason, cancellationToken);
        }

        var device = await dbContext.TrustedDevices
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == normalizedDevice, cancellationToken);
        if (device is not null && device.Status != TrustedDeviceStatus.Revoked)
        {
            device.Status = TrustedDeviceStatus.Revoked;
            device.RevokedReason = Truncate(reason, 200);
            device.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            device.RevokedByActorId = actorAccessor.Current.ActorId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return sids.Count;
    }

    public async Task<int> RevokePriorSessionsForDeviceAsync(
        Guid userId, string? deviceId, string excludeSid, string reason, CancellationToken cancellationToken)
    {
        var normalizedDevice = Normalize(deviceId);
        if (normalizedDevice is null)
        {
            return 0;
        }

        var normalizedExclude = Normalize(excludeSid);
        var sids = await dbContext.Sessions.AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.DeviceId == normalizedDevice
                        && x.RevokedAtUtc == null
                        && x.Sid != normalizedExclude)
            .Select(x => x.Sid)
            .ToListAsync(cancellationToken);

        foreach (var sid in sids)
        {
            await RevokeAsync(sid, reason, cancellationToken);
        }

        return sids.Count;
    }

    public async Task<IReadOnlyList<SessionItem>> ListAsync(
        Guid? userId, bool includeRevoked, string? currentSid, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var onlineWindow = OnlineWindow;
        var normalizedCurrent = Normalize(currentSid);

        var query = dbContext.Sessions.AsNoTracking()
            .Join(dbContext.Users.AsNoTracking(),
                session => session.UserId,
                user => user.Id,
                (session, user) => new { session, user });

        if (userId is { } uid)
        {
            query = query.Where(x => x.session.UserId == uid);
        }
        if (!includeRevoked)
        {
            query = query.Where(x => x.session.RevokedAtUtc == null && x.session.ExpiresAtUtc > now);
        }

        var rows = await query
            .OrderByDescending(x => x.session.LastActivityAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

        var result = new List<SessionItem>(rows.Count);
        foreach (var row in rows)
        {
            var session = row.session;
            var state = new SessionState(
                session.Sid, session.UserId, session.DeviceId, session.LoginMethod, session.PasswordVerifiedAtUtc,
                session.StrongAuthAtUtc, session.StrongAuthMethod, session.LastActivityAtUtc,
                session.ExpiresAtUtc, session.RevokedAtUtc);

            result.Add(new SessionItem(
                session.Sid,
                session.UserId,
                row.user.UserName ?? string.Empty,
                row.user.DisplayName,
                session.DeviceId,
                DeviceInfoSummary.Parse(session.DeviceInfoJson),
                session.LoginMethod,
                ParseClients(session.ClientIdsJson),
                session.CreatedByIp,
                session.UserAgent,
                session.CreatedAtUtc,
                session.LastActivityAtUtc,
                session.ExpiresAtUtc,
                session.RevokedAtUtc is null && now - session.LastActivityAtUtc <= onlineWindow,
                session.RevokedAtUtc is not null,
                session.RevokedAtUtc,
                session.RevokedReason,
                session.RevokedAtUtc is not null ? 0 : EvaluateLevel(state, now),
                session.Sid == normalizedCurrent));
        }
        return result;
    }

    public async Task RecordClientAsync(string sid, string clientId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null || string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null)
        {
            return;
        }

        var clients = ParseClients(session.ClientIdsJson).ToList();
        if (clients.Contains(clientId, StringComparer.Ordinal))
        {
            return;
        }

        clients.Add(clientId);
        session.ClientIdsJson = JsonSerializer.Serialize(clients, JsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantPasswordVerifiedAsync(string sid, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return;
        }

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        session.PasswordVerifiedAtUtc = now;
        session.LastActivityAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await stateCache.RemoveAsync(normalized, cancellationToken);
    }

    public async Task GrantStrongAuthAsync(string sid, string method, CancellationToken cancellationToken)
    {
        var normalized = Normalize(sid);
        if (normalized is null)
        {
            return;
        }

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        session.StrongAuthAtUtc = now;
        session.StrongAuthMethod = Truncate(method, 20);
        session.LastActivityAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await stateCache.RemoveAsync(normalized, cancellationToken);
    }

    // --- IDeviceTrustService ---

    public async Task<DeviceTrust> EvaluateAsync(Guid userId, string? deviceId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(deviceId);
        if (normalized is null)
        {
            return new DeviceTrust(false, false, DeviceTrustReasons.DeviceIdMissing);
        }

        var device = await dbContext.TrustedDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == normalized, cancellationToken);

        if (device is null)
        {
            return new DeviceTrust(false, false, DeviceTrustReasons.DeviceNew);
        }
        if (device.Status == TrustedDeviceStatus.Revoked)
        {
            return new DeviceTrust(true, false, DeviceTrustReasons.DeviceRevoked);
        }

        var decay = TimeSpan.FromDays(Math.Max(1, options.Value.Session.TrustedDeviceDecayDays));
        if (timeProvider.GetUtcNow().UtcDateTime - device.LastSeenUtc > decay)
        {
            return new DeviceTrust(true, false, DeviceTrustReasons.DeviceTrustExpired);
        }

        return new DeviceTrust(true, true, DeviceTrustReasons.DeviceTrusted);
    }

    public async Task RecordDeviceLoginAsync(
        Guid userId, string deviceId, string? deviceInfoJson, string? sid, bool grantTrust, CancellationToken cancellationToken)
    {
        var normalized = Normalize(deviceId);
        if (normalized is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var device = await dbContext.TrustedDevices
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == normalized, cancellationToken);

        if (device is null)
        {
            // 不授予信任就不建行：这张表只登记"被信任过"的设备，
            // 从未受信的设备在列表里由活跃会话合成，免得一次性设备把它撑大。
            if (!grantTrust)
            {
                return;
            }

            dbContext.TrustedDevices.Add(new AdminTrustedDevice
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                DeviceId = normalized,
                DeviceInfoJson = deviceInfoJson,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                LastLoginSid = Normalize(sid),
                Status = TrustedDeviceStatus.Trusted,
            });
        }
        else
        {
            // 最近使用时间与设备信息每次登录都刷新，与是否授予信任无关。
            device.DeviceInfoJson = deviceInfoJson ?? device.DeviceInfoJson;
            device.LastSeenUtc = now;
            device.LastLoginSid = Normalize(sid) ?? device.LastLoginSid;

            if (grantTrust)
            {
                device.Status = TrustedDeviceStatus.Trusted;
                device.RevokedReason = null;
                device.RevokedAtUtc = null;
                device.RevokedByActorId = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceItem>> ListDevicesAsync(
        Guid userId, string? currentDeviceId, string? currentSid, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var onlineWindow = OnlineWindow;
        var normalizedCurrentDevice = Normalize(currentDeviceId);
        var normalizedCurrentSid = Normalize(currentSid);

        var user = await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.UserName, x.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        var userName = user?.UserName ?? string.Empty;
        var displayName = user?.DisplayName ?? string.Empty;

        var devices = await dbContext.TrustedDevices.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastSeenUtc)
            .ToListAsync(cancellationToken);

        var sessions = await dbContext.Sessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.LastActivityAtUtc)
            .ToListAsync(cancellationToken);

        var sessionsByDevice = sessions
            .Where(x => x.DeviceId is not null)
            .GroupBy(x => x.DeviceId!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var result = new List<DeviceItem>(devices.Count + sessionsByDevice.Count);
        foreach (var device in devices)
        {
            var deviceSessions = sessionsByDevice.GetValueOrDefault(device.DeviceId, []);
            result.Add(new DeviceItem(
                device.DeviceId,
                DeviceInfoSummary.Parse(device.DeviceInfoJson),
                device.FirstSeenUtc,
                device.LastSeenUtc,
                now - device.LastSeenUtc <= onlineWindow || deviceSessions.Any(x => now - x.LastActivityAtUtc <= onlineWindow),
                device.Status == TrustedDeviceStatus.Trusted,
                deviceSessions.Count,
                deviceSessions
                    .Select(x => ToSessionItem(x, userId, userName, displayName, normalizedCurrentSid, now, onlineWindow))
                    .ToList(),
                device.DeviceId == normalizedCurrentDevice,
                device.Status,
                device.RevokedAtUtc,
                device.RevokedReason));
        }

        // 没走完强认证的设备不会进受信表，但用户此刻可能正从它登录；
        // 只遍历受信表会让"我的设备"对这类账号永远是空的，所以按活跃会话补齐。
        foreach (var (deviceId, deviceSessions) in sessionsByDevice)
        {
            if (devices.Any(x => x.DeviceId == deviceId))
            {
                continue;
            }
            result.Add(new DeviceItem(
                deviceId,
                DeviceInfoSummary.Parse(deviceSessions[0].DeviceInfoJson),
                deviceSessions.Min(x => x.CreatedAtUtc),
                deviceSessions.Max(x => x.LastActivityAtUtc),
                deviceSessions.Any(x => now - x.LastActivityAtUtc <= onlineWindow),
                IsTrusted: false,
                deviceSessions.Count,
                deviceSessions
                    .Select(x => ToSessionItem(x, userId, userName, displayName, normalizedCurrentSid, now, onlineWindow))
                    .ToList(),
                deviceId == normalizedCurrentDevice,
                TrustedDeviceStatus.NeverTrusted,
                RevokedAtUtc: null,
                RevokedReason: null));
        }

        return result.OrderByDescending(x => x.LastSeenUtc).ToList();
    }

    public async Task<IReadOnlyList<DeviceUsageItem>> ListDeviceUsageAsync(string deviceId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(deviceId);
        if (normalized is null)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var onlineWindow = OnlineWindow;

        var trustRows = await dbContext.TrustedDevices.AsNoTracking()
            .Where(x => x.DeviceId == normalized)
            .ToListAsync(cancellationToken);

        // 会话表才是主证据：降级密码登录的设备根本不会进受信表。
        var sessionGroups = await dbContext.Sessions.AsNoTracking()
            .Where(x => x.DeviceId == normalized)
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                SessionCount = g.Count(),
                LastSessionAtUtc = g.Max(x => x.LastActivityAtUtc),
                FirstSessionAtUtc = g.Min(x => x.CreatedAtUtc),
            })
            .ToListAsync(cancellationToken);

        var userIds = trustRows.Select(x => x.UserId)
            .Union(sessionGroups.Select(x => x.UserId))
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
        {
            return [];
        }

        var users = await dbContext.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.UserName, x.DisplayName })
            .ToListAsync(cancellationToken);
        var userById = users.ToDictionary(x => x.Id);
        var trustByUser = trustRows.ToDictionary(x => x.UserId);
        var sessionByUser = sessionGroups.ToDictionary(x => x.UserId);

        var result = new List<DeviceUsageItem>(userIds.Count);
        foreach (var userId in userIds)
        {
            var trust = trustByUser.GetValueOrDefault(userId);
            var aggregate = sessionByUser.GetValueOrDefault(userId);
            userById.TryGetValue(userId, out var user);

            var firstSeen = trust?.FirstSeenUtc ?? aggregate?.FirstSessionAtUtc ?? now;
            var lastSeen = trust is not null && aggregate is not null
                ? (trust.LastSeenUtc > aggregate.LastSessionAtUtc ? trust.LastSeenUtc : aggregate.LastSessionAtUtc)
                : trust?.LastSeenUtc ?? aggregate?.LastSessionAtUtc ?? now;

            result.Add(new DeviceUsageItem(
                normalized,
                userId,
                user?.UserName ?? "(已删除用户)",
                user?.DisplayName ?? string.Empty,
                trust?.Status ?? TrustedDeviceStatus.NeverTrusted,
                firstSeen,
                lastSeen,
                trust?.RevokedAtUtc,
                trust?.RevokedReason,
                aggregate?.SessionCount ?? 0,
                aggregate?.LastSessionAtUtc,
                now - lastSeen <= onlineWindow));
        }

        return result.OrderByDescending(x => x.LastSeenUtc).ToList();
    }

    public async Task RevokeDeviceAsync(Guid userId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        await RevokeAllForDeviceAsync(userId, deviceId, reason, cancellationToken);
    }

    // --- helpers ---

    private TimeSpan OnlineWindow =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.Session.OnlineWindowMinutes));

    private TimeSpan PasswordGapWindow =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.Session.PasswordActivityGapMinutes));

    private TimeSpan StrongAuthWindow =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.Session.StrongAuthWindowMinutes));

    /// <summary>
    /// 事实缓存的存活时间：取在线窗口与会话剩余寿命的较小值。
    /// 吊销不靠 TTL 兜底（有独立的只写标记），所以这里只需要保证陈旧事实不会长期驻留。
    /// </summary>
    private TimeSpan CacheTtl(SessionState state, DateTime now)
    {
        var remaining = state.ExpiresAtUtc - now;
        var cap = OnlineWindow;
        return remaining < cap ? remaining : cap;
    }

    private static SessionState ToState(AdminSession session) => new(
        session.Sid, session.UserId, session.DeviceId, session.LoginMethod,
        session.PasswordVerifiedAtUtc, session.StrongAuthAtUtc, session.StrongAuthMethod,
        session.LastActivityAtUtc, session.ExpiresAtUtc, session.RevokedAtUtc);

    /// <summary>只回写活跃时间与可能已清除的密码核验事实，不加载实体、不碰其它列。</summary>
    private Task WriteActivityAsync(string sid, SessionState state, CancellationToken cancellationToken) =>
        dbContext.Sessions
            .Where(x => x.Sid == sid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastActivityAtUtc, state.LastActivityAtUtc)
                .SetProperty(x => x.PasswordVerifiedAtUtc, state.PasswordVerifiedAtUtc),
                cancellationToken);

    private int EvaluateLevel(SessionState state) => EvaluateLevel(state, timeProvider.GetUtcNow().UtcDateTime);

    private int EvaluateLevel(SessionState state, DateTime now) => SessionLevels.Evaluate(
        now, state.PasswordVerifiedAtUtc, state.StrongAuthAtUtc, state.LastActivityAtUtc,
        PasswordGapWindow, StrongAuthWindow);

    private SessionItem ToSessionItem(
        AdminSession session, Guid userId, string userName, string displayName,
        string? currentSid, DateTime now, TimeSpan onlineWindow)
    {
        var state = new SessionState(
            session.Sid, session.UserId, session.DeviceId, session.LoginMethod, session.PasswordVerifiedAtUtc,
            session.StrongAuthAtUtc, session.StrongAuthMethod, session.LastActivityAtUtc,
            session.ExpiresAtUtc, session.RevokedAtUtc);

        return new SessionItem(
            session.Sid, userId, userName, displayName, session.DeviceId,
            DeviceInfoSummary.Parse(session.DeviceInfoJson), session.LoginMethod,
            ParseClients(session.ClientIdsJson), session.CreatedByIp, session.UserAgent,
            session.CreatedAtUtc, session.LastActivityAtUtc, session.ExpiresAtUtc,
            session.RevokedAtUtc is null && now - session.LastActivityAtUtc <= onlineWindow,
            session.RevokedAtUtc is not null, session.RevokedAtUtc, session.RevokedReason,
            session.RevokedAtUtc is not null ? 0 : EvaluateLevel(state, now),
            session.Sid == currentSid);
    }

    private async Task RevokeRefreshTokensAsync(string sid, DateTime now, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(x => x.Sid == sid && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = now;
        }
    }

    internal static string NewSid() =>
        "sid_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    internal static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    internal static IReadOnlyList<string> ParseClients(string? clientIdsJson)
    {
        if (string.IsNullOrWhiteSpace(clientIdsJson))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(clientIdsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];
}

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
/// 会话（sid）与设备信任的权威实现。数据落 <c>Identity_Sessions</c> / <c>Identity_TrustedDevices</c>，
/// 进程内仅保留活跃时间的写节流缓存；多副本部署时需要把节流缓存换成共享存储。
/// </summary>
internal sealed class SessionService(
    AdminDbContext dbContext,
    IOptions<AdminIdentityOptions> options,
    ICurrentActorAccessor actorAccessor,
    ISecurityEventLogger securityEvents,
    IBackChannelLogoutNotifier logoutNotifier,
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

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Sid == normalized, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now)
        {
            return null;
        }

        var gap = now - session.LastActivityAtUtc;
        var passwordGap = PasswordGapWindow;

        // 掉窗粘性：间隔超过 L2 窗口就清除密码核验事实，且不会因为后续活动自动恢复。
        var droppedLevel = false;
        if (gap > passwordGap && session.PasswordVerifiedAtUtc is not null)
        {
            session.PasswordVerifiedAtUtc = null;
            droppedLevel = true;
        }

        var throttle = TimeSpan.FromSeconds(Math.Max(1, options.Value.Session.ActivityWriteThrottleSeconds));
        if (droppedLevel || gap > throttle)
        {
            session.LastActivityAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (droppedLevel)
            {
                logger.LogInformation("会话 {Sid} 活跃间隔 {GapMinutes} 分钟超过 L2 窗口，已降级为 L1。",
                    session.Sid, (int)gap.TotalMinutes);
            }
        }

        return SessionLevels.Evaluate(
            now, session.PasswordVerifiedAtUtc, session.StrongAuthAtUtc, session.LastActivityAtUtc,
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
    }

    // --- IDeviceTrustService ---

    public async Task<DeviceTrust> EvaluateAsync(Guid userId, string? deviceId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(deviceId);
        if (normalized is null)
        {
            return new DeviceTrust(false, false, "device_id_missing");
        }

        var device = await dbContext.TrustedDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == normalized, cancellationToken);

        if (device is null)
        {
            return new DeviceTrust(false, false, "device_new");
        }
        if (device.Status == TrustedDeviceStatus.Revoked)
        {
            return new DeviceTrust(true, false, "device_revoked");
        }

        var decay = TimeSpan.FromDays(Math.Max(1, options.Value.Session.TrustedDeviceDecayDays));
        if (timeProvider.GetUtcNow().UtcDateTime - device.LastSeenUtc > decay)
        {
            return new DeviceTrust(true, false, "device_trust_expired");
        }

        return new DeviceTrust(true, true, "device_trusted");
    }

    public async Task MarkTrustedAsync(
        Guid userId, string deviceId, string? deviceInfoJson, string? sid, CancellationToken cancellationToken)
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
            device.DeviceInfoJson = deviceInfoJson ?? device.DeviceInfoJson;
            device.LastSeenUtc = now;
            device.LastLoginSid = Normalize(sid) ?? device.LastLoginSid;
            device.Status = TrustedDeviceStatus.Trusted;
            device.RevokedReason = null;
            device.RevokedAtUtc = null;
            device.RevokedByActorId = null;
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

        var result = new List<DeviceItem>(devices.Count);
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
                device.DeviceId == normalizedCurrentDevice));
        }
        return result;
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

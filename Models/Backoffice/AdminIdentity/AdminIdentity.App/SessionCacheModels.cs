namespace AdminIdentity.App;

/// <summary>
/// 会话事实的热路径缓存（Redis）。<c>Identity_Sessions</c> 始终是权威：
/// 缓存不可用或未命中时一律退回直查 DB，绝不因为缓存故障把已登录用户判成无效。
/// <para>
/// 吊销与普通事实分开存：事实可以被并发读请求回填（因此可能短暂陈旧），
/// 而吊销标记只写不删，读请求永远不会覆盖它——这是"秒踢"在缓存存在时依然成立的原因。
/// </para>
/// </summary>
public interface ISessionStateCache
{
    /// <summary>读取会话事实；返回值的 <c>RevokedAtUtc</c> 恒为 null，吊销状态请用 <see cref="IsRevokedAsync"/> 判断。</summary>
    Task<SessionState?> TryGetAsync(string sid, CancellationToken cancellationToken);

    Task SetAsync(SessionState state, TimeSpan ttl, CancellationToken cancellationToken);

    Task RemoveAsync(string sid, CancellationToken cancellationToken);

    /// <summary>标记凭证已吊销。只写不删，TTL 取会话剩余寿命即可。</summary>
    Task MarkRevokedAsync(string sid, TimeSpan ttl, CancellationToken cancellationToken);

    Task<bool> IsRevokedAsync(string sid, CancellationToken cancellationToken);
}

/// <summary>
/// 二次验证挑战与验证码的暂存。放 Redis 而不是进程内存，
/// 是因为验证码必须在"发码请求"和"验码请求"可能落到不同副本时仍然有效，且重启不能把用户卡在流程中间。
/// </summary>
public interface IStepUpStateStore
{
    Task<StepUpChallengeState?> GetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, StepUpChallengeState state, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// 一次 step-up 挑战的全部状态。验证码只以 SHA256 形式存放，明文既不落库也不进缓存。
/// 属性可写是为了能在 Redis 与进程内存两种实现之间做 JSON 往返。
/// </summary>
public sealed class StepUpChallengeState
{
    public Guid UserId { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceInfoJson { get; set; }
    public string? Sid { get; set; }
    public DateTime? PasswordVerifiedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;

    private Dictionary<string, string> destinations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>通道名大小写不敏感；setter 重建字典，避免 JSON 往返后丢掉比较器。</summary>
    public Dictionary<string, string> Destinations
    {
        get => destinations;
        set => destinations = new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase);
    }

    public string? CodeHash { get; set; }
    public string? CodeChannel { get; set; }
    public DateTime CodeExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime? LastSentAtUtc { get; set; }
    public int SentCount { get; set; }
    public DateTime SentWindowStartUtc { get; set; }
}

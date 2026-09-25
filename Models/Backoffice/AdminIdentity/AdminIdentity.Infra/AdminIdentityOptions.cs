namespace AdminIdentity.Infra;

public sealed class AdminIdentityOptions
{
    public const string SectionName = "AdminIdentity";

    public JwtOptions Jwt { get; set; } = new();
    public BootstrapOptions Bootstrap { get; set; } = new();
    public SessionOptions Session { get; set; } = new();
    public StepUpOptions StepUp { get; set; } = new();
    public string DataProtectionKeyDirectory { get; set; } = Path.Combine("data", "admin-keys");

    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = "Admin.Host";
        public string Audience { get; set; } = "Admin.Host.Api";
        public string? SigningKey { get; set; }
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;

        /// <summary>
        /// 已吊销 refresh token 被重放时的宽限窗口（秒）。窗口内视为并发竞态，仅拒绝；
        /// 超过窗口判定为令牌泄露，吊销整条令牌链并记录安全事件。
        /// </summary>
        public int RefreshTokenReuseLeewaySeconds { get; set; } = 15;
    }

    public sealed class BootstrapOptions
    {
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Password { get; set; }
    }

    public sealed class SessionOptions
    {
        /// <summary>会话登记寿命（天），与 refresh token 寿命对齐。</summary>
        public int LifetimeDays { get; set; } = 14;

        /// <summary>"在线"判定窗口（分钟）。</summary>
        public int OnlineWindowMinutes { get; set; } = 15;

        /// <summary>L2 维持条件：连续活跃间隔上限（分钟），超过即掉窗且粘性失效。</summary>
        public int PasswordActivityGapMinutes { get; set; } = 60;

        /// <summary>L3 窗口：强认证后多少分钟内有效。</summary>
        public int StrongAuthWindowMinutes { get; set; } = 10;

        /// <summary>活跃时间写库节流（秒），避免每请求都产生一次 UPDATE。</summary>
        public int ActivityWriteThrottleSeconds { get; set; } = 60;

        /// <summary>受信设备多少天未登录后降级为需要 step-up。</summary>
        public int TrustedDeviceDecayDays { get; set; } = 90;
    }

    public sealed class StepUpOptions
    {
        /// <summary>stepUpToken 有效期（分钟）。</summary>
        public int ChallengeMinutes { get; set; } = 10;

        public int CodeLength { get; set; } = 6;
        public int CodeTtlMinutes { get; set; } = 5;
        public int MaxVerifyAttempts { get; set; } = 5;
        public int ResendCooldownSeconds { get; set; } = 60;
        public int MaxCodesPerHour { get; set; } = 10;

        /// <summary>仅本地开发：为 true 时把验证码写入日志。生产必须保持 false，验证码明文永不落库/落日志。</summary>
        public bool LogCodeForDevelopment { get; set; }

        /// <summary>未配置时退化为日志通道（不真正发信），便于本地联调。</summary>
        public SmtpOptions? Smtp { get; set; }
    }

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 25;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "noreply@m-tu.com";
        public string FromDisplayName { get; set; } = "MengtuV";
        public bool EnableSsl { get; set; }
    }
}

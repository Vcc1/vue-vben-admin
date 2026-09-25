using System.Net;
using System.Net.Mail;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminIdentity.Infra;

/// <summary>一次性验证码的投递通道。目标地址由各通道自行从用户资料解析，解析不到即视为该通道不可用。</summary>
public interface IOtpDeliveryChannel
{
    string Channel { get; }

    /// <summary>返回未脱敏的投递目标；null 表示该用户对当前通道不可用（如未绑定邮箱）。</summary>
    Task<string?> ResolveDestinationAsync(Guid userId, CancellationToken cancellationToken);

    Task SendAsync(string destination, string code, CancellationToken cancellationToken);
}

internal sealed class EmailOtpDeliveryChannel(
    AdminDbContext dbContext,
    IOptions<AdminIdentityOptions> options,
    ILogger<EmailOtpDeliveryChannel> logger)
    : IOtpDeliveryChannel
{
    public string Channel => StepUpChannels.EmailOtp;

    public async Task<string?> ResolveDestinationAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken) is { Length: > 0 } email
            ? email
            : null;
    }

    public async Task SendAsync(string destination, string code, CancellationToken cancellationToken)
    {
        var smtp = options.Value.StepUp.Smtp;
        if (string.IsNullOrWhiteSpace(smtp?.Host))
        {
            // 未配置 SMTP 时只记录投递事实，验证码本身仅在显式开启开发开关时才落日志。
            logger.LogWarning("未配置 AdminIdentity:StepUp:Smtp:Host，验证码未真正发送到 {Destination}。", Mask(destination));
            if (options.Value.StepUp.LogCodeForDevelopment)
            {
                logger.LogWarning("[开发模式] {Destination} 的验证码是 {Code}。", Mask(destination), code);
            }
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.From, smtp.FromDisplayName),
            Subject = "登录验证码",
            Body = $"您的登录验证码是 {code}，{options.Value.StepUp.CodeTtlMinutes} 分钟内有效。如果不是您本人操作，请忽略并联系管理员。",
            IsBodyHtml = false,
        };
        message.To.Add(destination);

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("已向 {Destination} 发送登录验证码。", Mask(destination));
    }

    internal static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }
        return $"{email[0]}***{email[at..]}";
    }
}

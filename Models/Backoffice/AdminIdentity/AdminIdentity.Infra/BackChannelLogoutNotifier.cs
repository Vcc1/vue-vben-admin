using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OidcServer.Infra;

namespace AdminIdentity.Infra;

/// <summary>
/// 按 OIDC Back-Channel Logout 1.0 向已签发过 token 的客户端推送 logout_token。
/// 推送失败不阻塞吊销：客户端仍会在 access token 自然过期后失效（≤15 分钟 + 30 秒容差）。
/// </summary>
internal sealed class BackChannelLogoutNotifier(
    AdminDbContext dbContext,
    IOptions<OidcServerOptions> oidcOptions,
    IHttpClientFactory httpClientFactory,
    ISecurityEventLogger securityEvents,
    TimeProvider timeProvider,
    ILogger<BackChannelLogoutNotifier> logger)
    : IBackChannelLogoutNotifier
{
    public const string LogoutEventUri = "http://schemas.openid.net/event/backchannel-logout";
    public const string HttpClientName = "OidcBackChannelLogout";

    /// <summary>规范要求的 events claim 载荷。</summary>
    private static readonly string LogoutEventsJson = "{\"" + LogoutEventUri + "\":{}}";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int MaxAttempts = 2;

    public async Task NotifyAsync(
        string sid, Guid userId, IReadOnlyList<string> clientIds, CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0)
        {
            return;
        }

        var targets = await dbContext.OidcApplications.AsNoTracking()
            .Where(x => x.BackChannelLogoutUri != null && x.ClientId != null && clientIds.Contains(x.ClientId))
            .Select(x => new { ClientId = x.ClientId!, Uri = x.BackChannelLogoutUri! })
            .ToListAsync(cancellationToken);

        if (targets.Count == 0)
        {
            return;
        }

        var credentials = OidcSigningKeyResolver.ResolveCredentials(oidcOptions.Value.SigningKey);
        if (credentials is null)
        {
            logger.LogWarning(
                "未配置持久化的 OidcServer:SigningKey，无法签发 logout_token，跳过 back-channel 推送。" +
                "被吊销的凭证将在 access token 过期后失效。");
            return;
        }

        foreach (var target in targets)
        {
            await PushAsync(target.ClientId, target.Uri, sid, userId, credentials, cancellationToken);
        }
    }

    private async Task PushAsync(
        string clientId,
        string uri,
        string sid,
        Guid userId,
        Microsoft.IdentityModel.Tokens.SigningCredentials credentials,
        CancellationToken cancellationToken)
    {
        var logoutToken = CreateLogoutToken(clientId, sid, userId, credentials);
        var client = httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RequestTimeout);

                using var content = new FormUrlEncodedContent([new("logout_token", logoutToken)]);
                using var response = await client.PostAsync(uri, content, timeout.Token);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("已向客户端 {ClientId} 推送 logout_token（sid={Sid}）。", clientId, sid);
                    return;
                }

                logger.LogWarning(
                    "客户端 {ClientId} 的 backchannel_logout_uri 返回 {StatusCode}（第 {Attempt} 次）。",
                    clientId, (int)response.StatusCode, attempt);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                logger.LogWarning(ex,
                    "向客户端 {ClientId} 推送 logout_token 失败（第 {Attempt} 次）。", clientId, attempt);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }

        await securityEvents.LogAsync(new SecurityEvent(
            "BackChannelLogoutPushFailed",
            "Medium",
            $"logout_token 推送失败，客户端 {clientId} 将在 access token 过期后才失效。",
            new { Sid = sid, ClientId = clientId, Uri = uri },
            ActorId: userId.ToString()),
            cancellationToken);
    }

    private string CreateLogoutToken(
        string clientId, string sid, Guid userId, Microsoft.IdentityModel.Tokens.SigningCredentials credentials)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(SessionClaims.Sid, sid),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            // logout_token 绝不能带 nonce，否则客户端必须拒绝。
            new("events", LogoutEventsJson, JsonClaimValueTypes.Json),
        };

        var token = new JwtSecurityToken(
            oidcOptions.Value.Issuer,
            clientId,
            claims,
            notBefore: now,
            expires: now.AddMinutes(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

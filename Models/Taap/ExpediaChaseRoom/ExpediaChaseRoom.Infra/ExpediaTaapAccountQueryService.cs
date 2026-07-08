using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaTaapAccountQueryService : IExpediaTaapAccountQueryService
{
    private readonly IExpediaTaapClient _taapClient;
    private readonly IExpediaTaapAccountSessionService _sessionService;
    private readonly ExpediaChaseRoomMetrics _metrics;
    private readonly ILogger<ExpediaTaapAccountQueryService> _logger;

    public ExpediaTaapAccountQueryService(
        IExpediaTaapClient taapClient,
        IExpediaTaapAccountSessionService sessionService,
        ExpediaChaseRoomMetrics metrics,
        ILogger<ExpediaTaapAccountQueryService> logger)
    {
        _taapClient = taapClient;
        _sessionService = sessionService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var loginState = await _sessionService.GetLoginStateAsync(account, cancellationToken);

        for (var attempt = 0; attempt < 9999; attempt++)
        {
            try
            {
                var startedAt = Stopwatch.GetTimestamp();
                try
                {
                    return await _taapClient.QueryPricesAsync(account.AccountId, loginState, request, cancellationToken);
                }
                finally
                {
                    _metrics.RecordOperationDuration(
                        account.AccountName,
                        "query",
                        Stopwatch.GetElapsedTime(startedAt));
                }
            }
            catch (ExpediaTaapLoginStateExpiredException ex) when (attempt == 0)
            {
                if (ex.ShouldRefreshSharedSha256Hash)
                {
                    _logger.LogWarning(
                        ex,
                        "账号 {AccountName} 的 Expedia TAAP 共享 sha256Hash 已失效，正在通过指定账号刷新后重试一次。",
                        account.AccountName);
                    await _sessionService.RefreshSharedSha256HashAsync(cancellationToken);
                    loginState = await _sessionService.GetLoginStateAsync(account, cancellationToken);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "账号 {AccountName} 的 Expedia TAAP 登录态已失效，正在刷新后重试一次。",
                        account.AccountName);
                    loginState = await _sessionService.RefreshLoginStateAsync(account, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"账号 {account.AccountId} 的 Expedia TAAP 请求重试流程异常结束。");
    }
}

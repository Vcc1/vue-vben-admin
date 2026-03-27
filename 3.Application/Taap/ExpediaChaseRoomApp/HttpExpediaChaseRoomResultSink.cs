using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Web.Taap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class HttpExpediaChaseRoomResultSink : IExpediaChaseRoomResultSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<ExpediaChaseRoomAppOptions> _optionsMonitor;
    private readonly IExpediaChaseRoomTaskStore _taskStore;
    private readonly IExpediaChaseRoomPricePersister _pricePersister;
    private readonly ExpediaChaseRoomMetrics _metrics;
    private readonly ILogger<HttpExpediaChaseRoomResultSink> _logger;

    public HttpExpediaChaseRoomResultSink(
        HttpClient httpClient,
        IOptionsMonitor<ExpediaChaseRoomAppOptions> optionsMonitor,
        IExpediaChaseRoomTaskStore taskStore,
        IExpediaChaseRoomPricePersister pricePersister,
        ExpediaChaseRoomMetrics metrics,
        ILogger<HttpExpediaChaseRoomResultSink> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _taskStore = taskStore;
        _pricePersister = pricePersister;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task OnSuccessAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        ExpediaTaapPriceQueryResult result,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (ExpediaChaseRoomResultAnalyzer.IsOffline(result.RawContent))
        {
            await _taskStore.MarkOfflineAsync(task, cancellationToken);
            _metrics.RecordTaskResult(account.AccountName, "offline");
            _logger.LogWarning(
                "酒店 {PropertyId} 被账号 {AccountName} 判定为下线。",
                task.PropertyId,
                account.AccountName);
            return;
        }

        _logger.LogInformation(
            "Expedia 追房完成开始回调。账号：{AccountName}，酒店：{PropertyId}，入住：{CheckIn:yyyy-MM-dd}，离店：{CheckOut:yyyy-MM-dd}。",
            account.AccountName,
            task.PropertyId,
            task.CheckIn,
            task.CheckOut);

        ExpediaChaseRoomPricePersistResult persistResult;
        var persistStartedAt = Stopwatch.GetTimestamp();
        try
        {
            persistResult = await _pricePersister.PersistAsync(account, task, result, cancellationToken);
        }
        finally
        {
            _metrics.RecordOperationDuration(
                account.AccountName,
                "persist",
                Stopwatch.GetElapsedTime(persistStartedAt));
        }

        await _taskStore.MarkCompletedAndLogAsync(task, account.WorkerKey, persistResult.HasPrice, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.PushQueryResultUrl) ||
            !string.IsNullOrWhiteSpace(options.NotifyExpediaCachePriceUrl))
        {
            var callbackStartedAt = Stopwatch.GetTimestamp();
            try
            {
                if (!string.IsNullOrWhiteSpace(options.PushQueryResultUrl))
                {
                    var callback = new ExpediaChaseRoomQueryCallbackInput
                    {
                        Task = task,
                        Result = result,
                        CompletedAt = DateTimeOffset.UtcNow
                    };
                    using var response = await _httpClient.PostAsJsonAsync(options.PushQueryResultUrl, callback, cancellationToken);
                    response.EnsureSuccessStatusCode();
                }

                if (!string.IsNullOrWhiteSpace(options.NotifyExpediaCachePriceUrl))
                {
                    var notifyPayload = new NotifyExpediaCacheInput
                    {
                        PropertyId = task.PropertyId,
                        MTHotelCode = task.MTHotelCode,
                        Adults = task.Adults,
                        Check =
                        [
                            new NotifyExpediaCacheCheckTime
                            {
                                CheckIn = task.CheckIn,
                                CheckOut = task.CheckOut
                            }
                        ]
                    };

                    using var response = await _httpClient.PostAsJsonAsync(
                        options.NotifyExpediaCachePriceUrl,
                        notifyPayload,
                        JsonOptions,
                        cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
            }
            finally
            {
                _metrics.RecordOperationDuration(
                    account.AccountName,
                    "success_callback",
                    Stopwatch.GetElapsedTime(callbackStartedAt));
            }
        }

        _metrics.RecordTaskResult(account.AccountName, persistResult.HasPrice ? "success" : "no_price");

        _logger.LogInformation(
            "Expedia 追房结果回调完成。账号：{AccountName}，酒店：{PropertyId}，入住：{CheckIn:yyyy-MM-dd}，离店：{CheckOut:yyyy-MM-dd}。",
            account.AccountName,
            task.PropertyId,
            task.CheckIn,
            task.CheckOut);
    }

    public async Task OnErrorAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        _metrics.RecordTaskResult(account.AccountName, "error");

        var options = _optionsMonitor.CurrentValue;
        if (!string.IsNullOrWhiteSpace(options.PushQueryErrorUrl))
        {
            var payload = new ExpediaChaseRoomQueryErrorCallbackInput
            {
                Task = task,
                ErrorMessage = exception.Message,
                FailedAt = DateTimeOffset.UtcNow
            };

            var callbackStartedAt = Stopwatch.GetTimestamp();
            try
            {
                await PushErrorAsync(account, options.PushQueryErrorUrl, payload, exception, cancellationToken);
            }
            finally
            {
                _metrics.RecordOperationDuration(
                    account.AccountName,
                    "error_callback",
                    Stopwatch.GetElapsedTime(callbackStartedAt));
            }

            return;
        }

        _logger.LogError(
            exception,
            "Expedia 追房任务失败。账号：{AccountName}，酒店：{PropertyId}，入住：{CheckIn:yyyy-MM-dd}，离店：{CheckOut:yyyy-MM-dd}。",
            account.AccountName,
            task.PropertyId,
            task.CheckIn,
            task.CheckOut);
    }

    private async Task PushErrorAsync(
        ExpediaTaapAccountWorkerContext account,
        string url,
        ExpediaChaseRoomQueryErrorCallbackInput payload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception callbackException)
        {
            _logger.LogError(
                callbackException,
                "Expedia 追房错误回调失败，原始错误：{ErrorMessage}",
                exception.Message);
        }

        _logger.LogError(
            exception,
            "Expedia 追房任务失败。账号：{AccountName}，酒店：{PropertyId}，入住：{CheckIn:yyyy-MM-dd}，离店：{CheckOut:yyyy-MM-dd}。",
            account.AccountName,
            payload.Task.PropertyId,
            payload.Task.CheckIn,
            payload.Task.CheckOut);
    }
}

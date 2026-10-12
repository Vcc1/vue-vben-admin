using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.HTTP.WorkWechatAlert;

public sealed class WorkWechatTemplateMessageApi : IWorkWechatTemplateMessageApi
{
    public const string HttpClientName = nameof(WorkWechatTemplateMessageApi);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static readonly Uri SendTemplateMsgUri = new("http://basicservice.m-tu.com/api/WorkWechat/SendTemplateMsg");

    static WorkWechatTemplateMessageApi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    readonly IHttpClientFactory _httpClientFactory;
    readonly ILogger<WorkWechatTemplateMessageApi> _logger;

    public WorkWechatTemplateMessageApi(
        IHttpClientFactory httpClientFactory,
        ILogger<WorkWechatTemplateMessageApi> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(WorkWechatTemplateMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SendTemplateMsgUri)
        {
            Content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"))
        };

        _logger.LogInformation("Sending WorkWechat alert API request. Body={RequestBody}", json);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "WorkWechat alert API failed. StatusCode={StatusCode}, RequestBody={RequestBody}, ResponseBody={ResponseBody}",
                (int)response.StatusCode,
                json,
                responseBody);
        }

        response.EnsureSuccessStatusCode();
        _logger.LogInformation(
            "WorkWechat alert API accepted. RequestBody={RequestBody}, ResponseBody={ResponseBody}",
            json,
            responseBody);
        EnsureBusinessSuccess(responseBody);
    }

    static void EnsureBusinessSuccess(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return;
        }

        WorkWechatTemplateMessageResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<WorkWechatTemplateMessageResponse>(responseBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"WorkWechat alert API returned invalid JSON. Body={responseBody}", ex);
        }

        if (result is null)
        {
            throw new InvalidOperationException($"WorkWechat alert API returned empty JSON. Body={responseBody}");
        }

        if (result.IsSuccess == false || result.Code <= 0)
        {
            throw new InvalidOperationException(
                $"WorkWechat alert API business failed. Code={result.Code}, Msg={result.Msg}, Body={responseBody}");
        }
    }

    static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var charset = response.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim('"')).GetString(bytes);
            }
            catch (ArgumentException)
            {
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    sealed class WorkWechatTemplateMessageResponse
    {
        public string? Data { get; set; }
        public bool? IsSuccess { get; set; }
        public string? Msg { get; set; }
        public int? Code { get; set; }
    }
}

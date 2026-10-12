using Framework.Core.IOC;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.Infra;
using HotelMatchingPipeline.Domain.Hotels;
using Infrastructure.SupplierStaticData;
using Infrastructure.Redis.HotelMatchingRedis;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Sql.RoomMapping;
using NLog.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;
using Tests.Host.DebugTools;
using Tests.Host.DebugTools.Contracts;
using VBaseFramework.Core.EF.Log;
using VBaseFramework.Core.NLog;
using RoomMatch.Core;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using RoomMatch.Core.RoomDictionaryDataSource;
using RoomMatch.Core.RoomMatch;
using RoomMatch.Core.RoomMatchingStrategies;
using OTADomain.Matchings;
using FromServicesAttribute = Microsoft.AspNetCore.Mvc.FromServicesAttribute;
using Tests.Host.HotelMappingSync;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddNLog("Configs/nlog.config");
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("MengTu.Connectmate.Client", LogLevel.Warning);
builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.IConnectmateConfigurationPullClient",
    LogLevel.Warning);

builder.Services.AddHttpClient();
builder.Services.AddLogging();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<EFLoggerProvider>();
builder.Services.AddScoped<ILogContextService, LogContextService>();
builder.Services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
if (!builder.Configuration.GetValue<bool>("Connectmate:SkipRemoteConfiguration"))
{
    builder.Services.AddConnectmateRemoteConfiguration(builder.Configuration);
}
builder.Services.Configure<JsonOptions>(options =>
{
    var json = options.SerializerOptions;
    json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    json.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    json.PropertyNameCaseInsensitive = true;
    json.WriteIndented = true;
    json.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

builder.Services.AddBusiness([typeof(RoomMappingDbContext).Assembly]);
builder.Services.AddHotelMatchingApplication(builder.Configuration);
builder.Services.AddHotelMatchingInfrastructure(builder.Configuration);
builder.Services.AddScoped<IHotelMappingSourceReader, HotelMappingSourceReader>();
builder.Services.AddScoped<HotelMappingSyncRunner>();
builder.Services.AddSingleton<HotelMappingRedisSyncWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<HotelMappingRedisSyncWorker>());
builder.Services.RemoveAll<HotelCleaner>();
builder.Services.RemoveAll<HotelCleaningService>();
builder.Services.RemoveAll<HotelMatchingService>();
builder.Services.RemoveAll<HotelDebugService>();
builder.Services.AddSupplierStaticDataInfrastructure(builder.Configuration);
builder.Services.AddRoomMatchInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IRoomWordDictionary, RoomDictionary>();
builder.Services.AddSingleton<OfficialDataSource>();
builder.Services.AddSingleton<ContrastOfficialDataSource>();
builder.Services.AddSingleton<IRoomAnalyzer, RoomAnalyzer>();
builder.Services.AddSingleton<RoomAnalyzer>(static provider => (RoomAnalyzer)provider.GetRequiredService<IRoomAnalyzer>());
builder.Services.AddSingleton<StrictEqualMatcher>();
builder.Services.AddSingleton<FullSequenceMatcher>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/", () => Results.Content(
    DebugToolShellPage.Render("房型匹配调试", "/room-match", RoomMatchPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/supplier-static-data", () => Results.Content(
    DebugToolShellPage.Render("静态数据查询调试", "/supplier-static-data", SupplierStaticDataPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/room-match", () => Results.Content(
    DebugToolShellPage.Render("房型匹配调试", "/room-match", RoomMatchPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/hotel-match-results", () => Results.Content(
    DebugToolShellPage.Render("酒店匹配结果", "/hotel-match-results", HotelMatchDataPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/hotel-match-conflicts", () => Results.Content(
    DebugToolShellPage.Render("酒店重复候选", "/hotel-match-conflicts", HotelMatchDataPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/hotel-match-deliveries", () => Results.Content(
    DebugToolShellPage.Render("酒店匹配投递", "/hotel-match-deliveries", HotelMatchDataPage.Html),
    "text/html; charset=utf-8"));
app.MapGet("/hotel-match-redis-sync", () => Results.Content(
    DebugToolShellPage.Render("酒店 Redis 同步", "/hotel-match-redis-sync", HotelMappingRedisSyncPage.Html),
    "text/html; charset=utf-8"));

app.MapPost("/api/query", ExecuteQueryAsync);
app.MapPost("/api/room-match/evaluate", EvaluateRoomMatchAsync);
app.MapPost("/api/hotel-mapping-redis-sync", (HotelMappingRedisSyncWorker worker) =>
    worker.TryStart(out var status)
        ? Results.Accepted(value: status)
        : Results.Conflict(status));
app.MapGet("/api/hotel-mapping-redis-sync/status", (HotelMappingRedisSyncWorker worker) =>
    Results.Ok(worker.GetStatus()));
app.MapGet("/api/hotel-mapping-redis-sync/connection", async (
    IHotelMappingRedisStore redisStore,
    CancellationToken cancellationToken) =>
{
    var mappingLatency = await redisStore.PingAsync(cancellationToken);
    var connectionInfo = redisStore.GetConnectionInfo();
    return Results.Ok(new
    {
        connected = true,
        latencyMilliseconds = mappingLatency.TotalMilliseconds,
        database = connectionInfo.Database,
        endpoints = connectionInfo.Endpoints
    });
});
app.MapControllers();
app.MapHealthChecks("/alive");

await app.RunAsync();

static async Task<IResult> ExecuteQueryAsync(
    SupplierStaticDataQueryRequest request,
    ISupplierStaticDataService supplierStaticDataService,
    IOptions<JsonOptions> jsonOptions,
    CancellationToken cancellationToken)
{
    if (request.SupId <= 0)
    {
        return Results.BadRequest(new { message = "SupId must be greater than 0." });
    }

    var hasSupplierHotelCode = !string.IsNullOrWhiteSpace(request.SupplierHotelCode);
    var hasMtHotelCode = !string.IsNullOrWhiteSpace(request.MTHotelCode);
    if (hasSupplierHotelCode == hasMtHotelCode)
    {
        return Results.BadRequest(new { message = "Fill exactly one of SHotelCode or MTHotelCode." });
    }

    var requestedCodes = SplitCodes(hasSupplierHotelCode ? request.SupplierHotelCode : request.MTHotelCode);
    if (requestedCodes.Length == 0)
    {
        return Results.BadRequest(new { message = "At least one hotel code is required." });
    }

    var options = new SupplierStaticDataRequestOptions
    {
        ForceRefresh = request.ForceRefresh,
        IncludeRawInfo = request.IncludeRawInfo,
        Language = request.Language
    };

    var normalizedQueryKind = request.QueryKind?.Trim().ToLowerInvariant();
    object result;
    switch (normalizedQueryKind)
    {
        case "hotel":
            result = await QueryAsync(
                requestedCodes,
                hasSupplierHotelCode,
                code => supplierStaticDataService.GetHotelBySupplierHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetHotelsBySupplierHotelCodesAsync(request.SupId, codes, options, cancellationToken),
                code => supplierStaticDataService.GetHotelByMTHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetHotelsByMTHotelCodesAsync(request.SupId, codes, options, cancellationToken));
            break;
        case "rooms":
            result = await QueryAsync(
                requestedCodes,
                hasSupplierHotelCode,
                code => supplierStaticDataService.GetRoomsBySupplierHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetRoomsBySupplierHotelCodesAsync(request.SupId, codes, options, cancellationToken),
                code => supplierStaticDataService.GetRoomsByMTHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetRoomsByMTHotelCodesAsync(request.SupId, codes, options, cancellationToken));
            break;
        case "hotelandrooms":
        case "hotel-and-rooms":
        case "hotel_and_rooms":
        case null:
        case "":
            result = await QueryAsync(
                requestedCodes,
                hasSupplierHotelCode,
                code => supplierStaticDataService.GetHotelAndRoomsBySupplierHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetHotelAndRoomsBySupplierHotelCodesAsync(request.SupId, codes, options, cancellationToken),
                code => supplierStaticDataService.GetHotelAndRoomsByMTHotelCodeAsync(request.SupId, code, options, cancellationToken),
                codes => supplierStaticDataService.GetHotelAndRoomsByMTHotelCodesAsync(request.SupId, codes, options, cancellationToken));
            break;
        default:
            return Results.BadRequest(new { message = $"Unsupported query kind '{request.QueryKind}'." });
    }

    var response = new SupplierStaticDataQueryResponse
    {
        SupId = request.SupId,
        QueryKind = string.IsNullOrWhiteSpace(request.QueryKind) ? "hotelAndRooms" : request.QueryKind.Trim(),
        CodeSource = hasSupplierHotelCode ? "supplierHotelCode" : "mtHotelCode",
        RequestedCode = string.Join(",", requestedCodes),
        RequestedCodes = requestedCodes,
        ForceRefresh = request.ForceRefresh,
        IncludeRawInfo = request.IncludeRawInfo,
        Language = options.GetLanguageOrDefault(),
        QueriedAt = DateTimeOffset.Now,
        Result = result
    };

    return Results.Json(response, jsonOptions.Value.SerializerOptions);
}

static IResult EvaluateRoomMatchAsync(
    RoomMatchEvaluateRequest request,
    [FromServices] RoomAnalyzer roomAnalyzer,
    [FromServices] StrictEqualMatcher strictEqualMatcher,
    [FromServices] FullSequenceMatcher fullSequenceMatcher,
    [FromServices] IOptions<JsonOptions> jsonOptions)
{
    var leftRoomName = request.LeftRoomName?.Trim();
    var rightRoomName = request.RightRoomName?.Trim();

    if (string.IsNullOrWhiteSpace(leftRoomName) || string.IsNullOrWhiteSpace(rightRoomName))
    {
        return Results.BadRequest(new { message = "LeftRoomName and RightRoomName are required." });
    }

    var setting = RoomMatchMapper.BuildSetting(request);
    var leftContext = roomAnalyzer.Analysis(new RoomContext(leftRoomName, setting));
    var rightContext = roomAnalyzer.Analysis(new RoomContext(rightRoomName, setting));

    // Online RoomMatching calls RoomMatch_A as OTA -> Supplier.
    // This tester keeps left=Supplier, right=OTA in the UI, so the invocation
    // order must be reversed here to stay consistent with production output.
    var strictEqualResult = strictEqualMatcher.Match(leftContext, rightContext);
    var fullSequenceResult = fullSequenceMatcher.Match(leftContext, rightContext);

    var (status, distance) = RoomMatch_A.RoomMatch2(rightContext, leftContext);
    var onlineResult = new RoomMatcherResultView
    {
        MatcherName = "Online RoomMatch_A",
        IsMatch = status != RoomMatchStatus.NotMatch,
        MatchType = (int)status,
        MatchScore = distance
    };

    var matcherResults = new[]
    {
        new RoomMatcherResultView
        {
            MatcherName = $"OTARoomBuilder {strictEqualResult.MatcherName}",
            IsMatch = strictEqualResult.IsMatch,
            MatchType = strictEqualResult.MatchType,
            MatchScore = strictEqualResult.MatchScore
        },
        new RoomMatcherResultView
        {
            MatcherName = $"OTARoomBuilder {fullSequenceResult.MatcherName}",
            IsMatch = fullSequenceResult.IsMatch,
            MatchType = fullSequenceResult.MatchType,
            MatchScore = fullSequenceResult.MatchScore
        },
        onlineResult
    };

    var bestMatch = onlineResult;
    var canMatch = matcherResults.Any(static result => result.IsMatch);
    var response = new RoomMatchEvaluateResponse
    {
        CanMatch = canMatch,
        Summary = canMatch
            ? $"Online RoomMatch_A => {status} ({(int)status}), boundaryDistance: {distance}, direction: OTA -> Supplier"
            : "Online RoomMatch_A rejected this room pair.",
        OnlineMatchType = (int)status,
        OnlineMatchTypeName = status.ToString(),
        BoundaryDistance = distance,
        MatchDirection = "ota->supplier",
        ComparedAt = DateTimeOffset.Now,
        Left = RoomMatchMapper.ToView(leftContext),
        Right = RoomMatchMapper.ToView(rightContext),
        BestMatch = bestMatch,
        MatcherResults = matcherResults
    };

    return Results.Json(response, jsonOptions.Value.SerializerOptions);
}

static string[] SplitCodes(string? input)
{
    return (input ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(static code => !string.IsNullOrWhiteSpace(code))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static async Task<object> QueryAsync<T>(
    IReadOnlyCollection<string> codes,
    bool hasSupplierHotelCode,
    Func<string, Task<SupplierStaticDataResult<T>>> singleSupplierQuery,
    Func<IReadOnlyCollection<string>, Task<IReadOnlyDictionary<string, SupplierStaticDataResult<T>>>> batchSupplierQuery,
    Func<string, Task<SupplierStaticDataResult<T>>> singleMtQuery,
    Func<IReadOnlyCollection<string>, Task<IReadOnlyDictionary<string, SupplierStaticDataResult<T>>>> batchMtQuery)
{
    if (codes.Count == 1)
    {
        var code = codes.First();
        return hasSupplierHotelCode
            ? await singleSupplierQuery(code)
            : await singleMtQuery(code);
    }

    return hasSupplierHotelCode
        ? await batchSupplierQuery(codes)
        : await batchMtQuery(codes);
}

file sealed class SupplierStaticDataQueryRequest
{
    public int SupId { get; set; }

    public string? SupplierHotelCode { get; set; }

    public string? MTHotelCode { get; set; }

    public string? QueryKind { get; set; } = "hotelAndRooms";

    public string? Language { get; set; }

    public bool ForceRefresh { get; set; }

    public bool IncludeRawInfo { get; set; }
}

file sealed class SupplierStaticDataQueryResponse
{
    public int SupId { get; set; }

    public string QueryKind { get; set; } = string.Empty;

    public string CodeSource { get; set; } = string.Empty;

    public string RequestedCode { get; set; } = string.Empty;

    public IReadOnlyList<string> RequestedCodes { get; set; } = [];

    public bool ForceRefresh { get; set; }

    public bool IncludeRawInfo { get; set; }

    public string Language { get; set; } = "en";

    public DateTimeOffset QueriedAt { get; set; }

    public object? Result { get; set; }
}

file static class IndexPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Supplier Static Data Tester</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f3efe6;
            --panel: rgba(255, 255, 255, 0.92);
            --text: #1f2937;
            --muted: #667085;
            --line: rgba(15, 23, 42, 0.12);
            --brand: #0f766e;
            --brand-strong: #115e59;
            --danger: #b42318;
            --shadow: 0 24px 60px rgba(15, 23, 42, 0.12);
        }

        * {
            box-sizing: border-box;
        }

        body {
            margin: 0;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
            background:
                radial-gradient(circle at top left, rgba(15, 118, 110, 0.18), transparent 30%),
                radial-gradient(circle at right, rgba(180, 83, 9, 0.12), transparent 28%),
                linear-gradient(160deg, #f6f4ed 0%, #efe8db 100%);
            color: var(--text);
            min-height: 100vh;
        }

        .layout {
            max-width: 1200px;
            margin: 0 auto;
            padding: 32px 20px 48px;
        }

        .hero {
            margin-bottom: 24px;
        }

        .eyebrow {
            display: inline-block;
            padding: 6px 10px;
            border-radius: 999px;
            background: rgba(15, 118, 110, 0.1);
            color: var(--brand-strong);
            font-size: 12px;
            letter-spacing: 0.08em;
            text-transform: uppercase;
        }

        h1 {
            margin: 16px 0 8px;
            font-size: clamp(32px, 5vw, 52px);
            line-height: 1;
        }

        .subtext {
            margin: 0;
            color: var(--muted);
            max-width: 760px;
            line-height: 1.6;
        }

        .grid {
            display: grid;
            gap: 20px;
            grid-template-columns: minmax(320px, 420px) minmax(0, 1fr);
            align-items: start;
        }

        .card {
            background: var(--panel);
            border: 1px solid var(--line);
            border-radius: 24px;
            box-shadow: var(--shadow);
            backdrop-filter: blur(12px);
        }

        .form-card {
            padding: 24px;
            position: sticky;
            top: 24px;
        }

        .result-card {
            padding: 0;
            overflow: hidden;
        }

        .section-title {
            margin: 0 0 16px;
            font-size: 20px;
        }

        .field {
            margin-bottom: 16px;
        }

        .field label {
            display: block;
            margin-bottom: 8px;
            font-size: 14px;
            font-weight: 600;
        }

        .field input,
        .field select {
            width: 100%;
            border: 1px solid rgba(15, 23, 42, 0.16);
            background: rgba(255, 255, 255, 0.96);
            color: var(--text);
            border-radius: 14px;
            padding: 12px 14px;
            font-size: 14px;
            outline: none;
            transition: border-color 0.15s ease, box-shadow 0.15s ease;
        }

        .field input:focus,
        .field select:focus {
            border-color: rgba(15, 118, 110, 0.72);
            box-shadow: 0 0 0 4px rgba(15, 118, 110, 0.14);
        }

        .field-hint {
            margin-top: 8px;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.5;
        }

        .checkbox-row {
            display: flex;
            align-items: center;
            gap: 10px;
            margin: 18px 0 20px;
        }

        .checkbox-row input {
            width: 18px;
            height: 18px;
        }

        button {
            width: 100%;
            border: none;
            border-radius: 16px;
            padding: 14px 18px;
            font-size: 15px;
            font-weight: 700;
            color: white;
            background: linear-gradient(135deg, var(--brand) 0%, #155eef 100%);
            cursor: pointer;
            transition: transform 0.12s ease, box-shadow 0.12s ease, opacity 0.12s ease;
            box-shadow: 0 18px 30px rgba(21, 94, 239, 0.22);
        }

        button:hover {
            transform: translateY(-1px);
        }

        button:disabled {
            opacity: 0.6;
            cursor: wait;
            transform: none;
        }

        .result-head {
            display: flex;
            justify-content: space-between;
            gap: 16px;
            padding: 20px 24px;
            border-bottom: 1px solid var(--line);
            background: linear-gradient(180deg, rgba(255,255,255,0.75), rgba(255,255,255,0.4));
        }

        .result-title {
            margin: 0;
            font-size: 18px;
        }

        .status {
            font-size: 13px;
            color: var(--muted);
        }

        .status.error {
            color: var(--danger);
        }

        pre {
            margin: 0;
            padding: 24px;
            min-height: 560px;
            overflow: auto;
            background: rgba(255, 255, 255, 0.7);
            color: #0f172a;
            font-size: 13px;
            line-height: 1.6;
            white-space: pre-wrap;
            word-break: break-word;
        }

        @media (max-width: 960px) {
            .grid {
                grid-template-columns: 1fr;
            }

            .form-card {
                position: static;
            }

            pre {
                min-height: 420px;
            }
        }
    </style>
</head>
<body>
    <main class="layout">
        <section class="hero">
            <span class="eyebrow">SupplierStaticDataApp Tester</span>
            <h1>统一静态信息查询测试台</h1>
            <p class="subtext">直接测试 <code>ISupplierStaticDataService</code>。支持填 <code>SHotelCode</code> 或 <code>MTHotelCode</code>，按 <code>SupId</code> 查询酒店、房型，或酒店加房型统一结果。</p>
        </section>

        <section class="grid">
            <form class="card form-card" id="query-form">
                <h2 class="section-title">查询参数</h2>

                <div class="field">
                    <label for="supId">SupId</label>
                    <input id="supId" name="supId" type="number" value="51" min="1" required />
                </div>

                <div class="field">
                    <label for="supplierHotelCode">SHotelCode</label>
                    <input id="supplierHotelCode" name="supplierHotelCode" type="text" placeholder="例如：123456,234567" />
                    <div class="field-hint">和 MTHotelCode 二选一；多个酒店 code 用英文逗号分隔。</div>
                </div>

                <div class="field">
                    <label for="mtHotelCode">MTHotelCode</label>
                    <input id="mtHotelCode" name="mtHotelCode" type="text" placeholder="例如：MT123456,MT234567" />
                </div>

                <div class="field">
                    <label for="language">Language</label>
                    <input id="language" name="language" type="text" placeholder="en" />
                    <div class="field-hint">为空或 null 时按 en 处理。</div>
                </div>

                <div class="field">
                    <label for="queryKind">查询内容</label>
                    <select id="queryKind" name="queryKind">
                        <option value="hotelAndRooms">酒店 + 房型</option>
                        <option value="hotel">酒店</option>
                        <option value="rooms">房型</option>
                    </select>
                </div>

                <label class="checkbox-row" for="forceRefresh">
                    <input id="forceRefresh" name="forceRefresh" type="checkbox" />
                    <span>ForceRefresh：跳过缓存直接刷新</span>
                </label>

                <label class="checkbox-row" for="includeRawInfo">
                    <input id="includeRawInfo" name="includeRawInfo" type="checkbox" />
                    <span>输出原始请求和响应</span>
                </label>

                <button id="submit-button" type="submit">发起查询</button>
            </form>

            <section class="card result-card">
                <div class="result-head">
                    <div>
                        <h2 class="result-title">返回结果</h2>
                        <div class="status" id="status-text">等待查询</div>
                    </div>
                </div>
                <pre id="result-view">{
  "message": "Submit a request to inspect supplier static data."
}</pre>
            </section>
        </section>
    </main>

    <script>
        const form = document.getElementById("query-form");
        const resultView = document.getElementById("result-view");
        const statusText = document.getElementById("status-text");
        const submitButton = document.getElementById("submit-button");

        form.addEventListener("submit", async (event) => {
            event.preventDefault();

            const payload = {
                supId: Number(document.getElementById("supId").value),
                supplierHotelCode: document.getElementById("supplierHotelCode").value.trim(),
                mtHotelCode: document.getElementById("mtHotelCode").value.trim(),
                queryKind: document.getElementById("queryKind").value,
                language: document.getElementById("language").value.trim(),
                forceRefresh: document.getElementById("forceRefresh").checked,
                includeRawInfo: document.getElementById("includeRawInfo").checked
            };

            submitButton.disabled = true;
            statusText.textContent = "查询中...";
            statusText.classList.remove("error");
            resultView.textContent = "Loading...";

            try {
                const response = await fetch("/api/query", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                const text = await response.text();
                let data;

                try {
                    data = text ? JSON.parse(text) : {};
                } catch {
                    data = { raw: text };
                }

                resultView.textContent = JSON.stringify(data, null, 2);
                if (!response.ok) {
                    statusText.textContent = `请求失败 (${response.status})`;
                    statusText.classList.add("error");
                } else {
                    statusText.textContent = "查询完成";
                }
            } catch (error) {
                statusText.textContent = "请求异常";
                statusText.classList.add("error");
                resultView.textContent = JSON.stringify({
                    message: error instanceof Error ? error.message : String(error)
                }, null, 2);
            } finally {
                submitButton.disabled = false;
            }
        });
    </script>
</body>
</html>
""";
}

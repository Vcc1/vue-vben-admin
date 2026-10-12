using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Infrastructure.Web.Taap;

public sealed class PlaywrightExpediaTaapAuthenticationService : IExpediaTaapAuthenticationService
{
    private static readonly TimeSpan LoginRetryDelay = TimeSpan.FromSeconds(10);

    private static readonly string[] RequiredCookieNames =
    [
        "currency", "MC1", "DUAID", "xdid", "s_fid", "eid", "aspp", "QuantumMetricUserID",
        "eg_adblock", "_cls_v", "tpid", "CRQS", "CRQSS", "s_ips",
        "AMCVS_C00802BE5330A8350A490D4C%40AdobeOrg", "s_ppvl", "s_cc", "csrfTokenL", "eg_ppid",
        "HMS", "ak_bmsc", "JSESSION", "linfo", "EG_SESSIONTOKEN", "iEAPID", "userGroupId", "s_ppv",
        "AMCV_C00802BE5330A8350A490D4C%40AdobeOrg", "__gads", "__gpi", "__eoi", "s_ppn", "s_tp",
        "user", "minfo", "accttype", "JSESSIONID", "cesc", "bm_sv", "_dd_s"
    ];

    private const string LocalBrowserMode = "Local";
    private const string RoomsAndRatesPropertyOffersOperationName = "RoomsAndRatesPropertyOffersQuery";
    private const string HotelInfoProbeUrl = "https://www.expediataap.cn/Zhuzhou-Hotels-Zhuzhou-Marriott-Hotel.h11672894.Hotel-Information?chkin=2026-04-16&chkout=2026-04-17";

    private readonly ILogger<PlaywrightExpediaTaapAuthenticationService> _logger;
    private readonly IOptionsMonitor<ExpediaTaapOptions> _optionsMonitor;

    public PlaywrightExpediaTaapAuthenticationService(
        ILogger<PlaywrightExpediaTaapAuthenticationService> logger,
        IOptionsMonitor<ExpediaTaapOptions> optionsMonitor)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<ExpediaTaapLoginState> LoginAsync(
        ExpediaTaapCredential credential,
        bool captureSharedSha256Hash = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var options = _optionsMonitor.CurrentValue;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await OpenBrowserAsync(playwright, options);

        var usesSharedCdpContext = IsRemoteCdpMode(options);
        IBrowserContext? browserContext = null;
        IPage? page = null;
        var closeBrowserContext = false;

        try
        {
            (browserContext, page, closeBrowserContext) = await OpenSessionAsync(browser, options, usesSharedCdpContext);
            await LoginWithCredentialsUntilSuccessAsync(browserContext, page, credential, options, cancellationToken);

            var sha256Hash = captureSharedSha256Hash
                ? await CaptureRoomsAndRatesSha256HashAsync(page, options, cancellationToken)
                : string.Empty;
            var cookies = await browserContext.CookiesAsync([options.BaseUrl]);
            if (!cookies.Any(x => x.Name == "user"))
            {
                throw new InvalidOperationException("Expedia TAAP 登录后未获取到名为 user 的 Cookie。");
            }

            var rawClientInfo = await ReadClientInfoAsync(page, options);
            var cookieMap = BuildCookieMap(cookies);
            var cookieHeader = string.Join("; ", cookieMap.Select(x => $"{x.Key}={x.Value}"));
            var eapid = int.TryParse(GetCookieValue(cookieMap, "iEAPID"), out var parsedEapid)
                ? parsedEapid
                : options.DefaultEapid;
            var duaid = GetCookieValue(cookieMap, "DUAID") ?? options.DefaultDuaid;
            var clientInfoToken = ExtractClientInfoToken(rawClientInfo, options.DefaultClientInfo);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(options.LoginCacheMinutes, 1));

            _logger.LogInformation("Expedia TAAP 登录成功，登录态过期时间：{ExpiresAt:O}。", expiresAt);

            return new ExpediaTaapLoginState
            {
                CookieHeader = cookieHeader,
                Sha256Hash = sha256Hash,
                ClientInfoToken = clientInfoToken,
                Duaid = duaid,
                Eapid = eapid,
                ExpiresAt = expiresAt
            };
        }
        finally
        {
            if (closeBrowserContext && browserContext is not null)
            {
                await browserContext.CloseAsync();
            }

            if (!closeBrowserContext && page is not null)
            {
                await page.CloseAsync();
            }
        }
    }

    public async Task<ExpediaTaapLoginState> RefreshSharedSha256HashAsync(
        ExpediaTaapCredential credential,
        ExpediaTaapLoginState? existingLoginState = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (existingLoginState is not null && CanReuseCookieForSharedSha(existingLoginState))
        {
            try
            {
                return await CaptureSharedSha256HashWithExistingLoginStateAsync(existingLoginState, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "使用缓存 Cookie 刷新 Expedia TAAP 共享 sha256Hash 失败，将回退到登录流程。");
            }
        }

        return await LoginAsync(credential, captureSharedSha256Hash: true, cancellationToken);
    }

    private async Task LoginWithCredentialsUntilSuccessAsync(
        IBrowserContext browserContext,
        IPage page,
        ExpediaTaapCredential credential,
        ExpediaTaapOptions options,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await browserContext.ClearCookiesAsync();
                await page.GotoAsync(options.GetLoginUrl(), new PageGotoOptions
                {
                    Timeout = (float)TimeSpan.FromSeconds(options.RequestTimeoutSeconds).TotalMilliseconds,
                    WaitUntil = WaitUntilState.Load
                });

                await page.Locator("#taapSigninEmailAddress").FillAsync(NormalizeSigninEmailAddress(credential.SigninEmailAddress));
                await page.Locator("#taapSigninPassword").FillAsync(credential.SigninPassword);
                await page.Locator("#submit-signin-button").ClickAsync();

                await WaitForUserCookieAsync(browserContext, options, cancellationToken);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                {
                    Timeout = (float)TimeSpan.FromSeconds(options.RequestTimeoutSeconds).TotalMilliseconds
                });

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expedia TAAP 账号密码登录失败，{DelaySeconds} 秒后重试。", LoginRetryDelay.TotalSeconds);
                await Task.Delay(LoginRetryDelay, cancellationToken);
            }
        }
    }

    private async Task<ExpediaTaapLoginState> CaptureSharedSha256HashWithExistingLoginStateAsync(
        ExpediaTaapLoginState existingLoginState,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await OpenBrowserAsync(playwright, options);

        var usesSharedCdpContext = IsRemoteCdpMode(options);
        IBrowserContext? browserContext = null;
        IPage? page = null;
        var closeBrowserContext = false;

        try
        {
            (browserContext, page, closeBrowserContext) = await OpenSessionAsync(browser, options, usesSharedCdpContext);
            await RestoreCookiesAsync(browserContext, existingLoginState.CookieHeader, options);

            var sha256Hash = await CaptureRoomsAndRatesSha256HashAsync(page, options, cancellationToken);
            return ApplySharedSha256Hash(existingLoginState, sha256Hash);
        }
        finally
        {
            if (closeBrowserContext && browserContext is not null)
            {
                await browserContext.CloseAsync();
            }

            if (!closeBrowserContext && page is not null)
            {
                await page.CloseAsync();
            }
        }
    }

    private static string NormalizeSigninEmailAddress(string signinEmailAddress)
    {
        return string.Equals(signinEmailAddress, "yitutravel011@126.com", StringComparison.OrdinalIgnoreCase)
            ? "yitutravel01@126.com"
            : signinEmailAddress;
    }

    private static bool CanReuseCookieForSharedSha(ExpediaTaapLoginState loginState)
    {
        return !string.IsNullOrWhiteSpace(loginState.CookieHeader) &&
               !string.IsNullOrWhiteSpace(loginState.ClientInfoToken) &&
               !string.IsNullOrWhiteSpace(loginState.Duaid) &&
               loginState.Eapid > 0;
    }

    private static ExpediaTaapLoginState ApplySharedSha256Hash(
        ExpediaTaapLoginState loginState,
        string sha256Hash)
    {
        return new ExpediaTaapLoginState
        {
            CookieHeader = loginState.CookieHeader,
            Sha256Hash = sha256Hash,
            ClientInfoToken = loginState.ClientInfoToken,
            Duaid = loginState.Duaid,
            Eapid = loginState.Eapid,
            ExpiresAt = loginState.ExpiresAt
        };
    }

    private static async Task RestoreCookiesAsync(
        IBrowserContext browserContext,
        string cookieHeader,
        ExpediaTaapOptions options)
    {
        await browserContext.ClearCookiesAsync();

        var cookies = ParseCookieHeader(cookieHeader)
            .Select(x => new Cookie
            {
                Name = x.Key,
                Value = x.Value,
                Url = options.BaseUrl
            })
            .ToArray();

        if (cookies.Length == 0)
        {
            throw new InvalidOperationException("缓存 Cookie 为空，无法刷新共享 sha256Hash。");
        }

        await browserContext.AddCookiesAsync(cookies);
    }

    private static IReadOnlyDictionary<string, string> ParseCookieHeader(string cookieHeader)
    {
        var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return cookies;
        }

        foreach (var segment in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2);
            var name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            cookies[name] = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        return cookies;
    }

    private static string? TryGetRoomsAndRatesSha256Hash(string? postData)
    {
        if (string.IsNullOrWhiteSpace(postData))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(postData);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty("operationName", out var operationNameElement))
                {
                    continue;
                }

                if (!string.Equals(
                        operationNameElement.GetString(),
                        RoomsAndRatesPropertyOffersOperationName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!item.TryGetProperty("extensions", out var extensionsElement))
                {
                    return null;
                }

                if (!extensionsElement.TryGetProperty("persistedQuery", out var persistedQueryElement))
                {
                    return null;
                }

                if (!persistedQueryElement.TryGetProperty("sha256Hash", out var sha256HashElement))
                {
                    return null;
                }

                return sha256HashElement.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string> CaptureRoomsAndRatesSha256HashAsync(
        IPage page,
        ExpediaTaapOptions options,
        CancellationToken cancellationToken)
    {
        var url = BuildHotelInfoUrlWithCurrentDates(HotelInfoProbeUrl);
        var timeout = TimeSpan.FromSeconds(Math.Max(options.RequestTimeoutSeconds, 1));
        var sha256HashTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IRequest>? requestHandler = null;

        requestHandler = (_, request) =>
        {
            if (!request.Url.Contains("/graphql", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var sha256Hash = TryGetRoomsAndRatesSha256Hash(request.PostData);
            if (string.IsNullOrWhiteSpace(sha256Hash))
            {
                return;
            }

            _logger.LogInformation("已捕获 Expedia TAAP sha256Hash：{Sha256Hash}", sha256Hash);

            if (sha256HashTcs.TrySetResult(sha256Hash) && requestHandler is not null)
            {
                page.Request -= requestHandler;
            }
        };

        page.Request += requestHandler;

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = (float)timeout.TotalMilliseconds,
                WaitUntil = WaitUntilState.Load
            });
            await EnsureNotRedirectedToLoginAsync(page, options, url);

            var completedTask = await Task.WhenAny(
                sha256HashTcs.Task,
                Task.Delay(timeout, cancellationToken));

            if (completedTask == sha256HashTcs.Task)
            {
                return await sha256HashTcs.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await EnsureNotRedirectedToLoginAsync(page, options, url);
            throw new TimeoutException(
                $"访问 {url} 时，在 {options.RequestTimeoutSeconds} 秒内未捕获到 {RoomsAndRatesPropertyOffersOperationName} 的 sha256Hash。");
        }
        finally
        {
            if (requestHandler is not null)
            {
                page.Request -= requestHandler;
            }
        }
    }

    private static async Task EnsureNotRedirectedToLoginAsync(
        IPage page,
        ExpediaTaapOptions options,
        string requestedUrl)
    {
        if (!IsLoginPageUrl(page.Url, options) && !await HasLoginFormAsync(page))
        {
            return;
        }

        throw new InvalidOperationException(
            $"使用缓存 Cookie 访问 {requestedUrl} 时被重定向到登录页，当前 Cookie 已失效。");
    }

    private static bool IsLoginPageUrl(string? url, ExpediaTaapOptions options)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUri))
        {
            return url.Contains("/taap/signin", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(options.GetLoginUrl(), UriKind.Absolute, out var loginUri))
        {
            return currentUri.AbsolutePath.Contains("/taap/signin", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(currentUri.Host, loginUri.Host, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   currentUri.AbsolutePath.TrimEnd('/'),
                   loginUri.AbsolutePath.TrimEnd('/'),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasLoginFormAsync(IPage page)
    {
        return await page.Locator("#submit-signin-button").CountAsync() > 0 ||
               await page.Locator("#taapSigninEmailAddress").CountAsync() > 0 ||
               await page.Locator("#taapSigninPassword").CountAsync() > 0;
    }

    private async Task<IBrowser> OpenBrowserAsync(IPlaywright playwright, ExpediaTaapOptions options)
    {
        if (IsLocalBrowserMode(options))
        {
            _logger.LogInformation("正在为 Expedia TAAP 启动本地 Chromium。");

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = options.Headless
            };

            if (!string.IsNullOrWhiteSpace(options.BrowserChannel))
            {
                launchOptions.Channel = options.BrowserChannel;
            }

            if (!string.IsNullOrWhiteSpace(options.ProxyServer))
            {
                launchOptions.Proxy = new Proxy
                {
                    Server = options.ProxyServer
                };
            }

            return await playwright.Chromium.LaunchAsync(launchOptions);
        }

        if (!string.IsNullOrWhiteSpace(options.RemoteBrowserWsEndpoint))
        {
            _logger.LogInformation("正在连接远程 Playwright 浏览器：{Endpoint}。", options.RemoteBrowserWsEndpoint);
            return await playwright.Chromium.ConnectAsync(options.RemoteBrowserWsEndpoint);
        }

        if (!string.IsNullOrWhiteSpace(options.RemoteBrowserCdpEndpoint))
        {
            _logger.LogInformation("正在连接远程 CDP 浏览器：{Endpoint}。", options.RemoteBrowserCdpEndpoint);
            return await playwright.Chromium.ConnectOverCDPAsync(options.RemoteBrowserCdpEndpoint);
        }

        throw new InvalidOperationException(
            "Expedia TAAP 未配置可用浏览器。请将 BrowserMode 设为 Local，或配置 RemoteBrowserWsEndpoint / RemoteBrowserCdpEndpoint。");
    }

    private static bool IsLocalBrowserMode(ExpediaTaapOptions options)
    {
        return string.Equals(options.BrowserMode, LocalBrowserMode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemoteCdpMode(ExpediaTaapOptions options)
    {
        return !IsLocalBrowserMode(options) && !string.IsNullOrWhiteSpace(options.RemoteBrowserCdpEndpoint);
    }

    private static async Task<(IBrowserContext BrowserContext, IPage Page, bool CloseBrowserContext)> OpenSessionAsync(
        IBrowser browser,
        ExpediaTaapOptions options,
        bool usesSharedCdpContext)
    {
        if (usesSharedCdpContext)
        {
            var browserContext = browser.Contexts.FirstOrDefault();
            if (browserContext is not null)
            {
                var sharedContextPage = await browserContext.NewPageAsync();
                return (browserContext, sharedContextPage, false);
            }

            var newContext = await CreateBrowserContextAsync(browser, options);
            var newPage = await newContext.NewPageAsync();
            return (newContext, newPage, true);
        }

        var context = await CreateBrowserContextAsync(browser, options);
        var page = await context.NewPageAsync();
        return (context, page, true);
    }

    private static Task<IBrowserContext> CreateBrowserContextAsync(IBrowser browser, ExpediaTaapOptions options)
    {
        return browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            Locale = "zh-CN",
            TimezoneId = "Asia/Shanghai",
            ViewportSize = new ViewportSize
            {
                Width = 1366,
                Height = 768
            },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36"
        });
    }

    private static async Task<bool> HasUserCookieAsync(IBrowserContext browserContext, string url)
    {
        var cookies = await browserContext.CookiesAsync([url]);
        return cookies.Any(x => x.Name == "user");
    }

    private static async Task WaitForUserCookieAsync(
        IBrowserContext browserContext,
        ExpediaTaapOptions options,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(options.RequestTimeoutSeconds);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await HasUserCookieAsync(browserContext, options.BaseUrl))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Expedia TAAP 登录超时，在规定时间内未获取到名为 user 的 Cookie。");
    }

    private static async Task<string?> ReadClientInfoAsync(IPage page, ExpediaTaapOptions options)
    {
        var clientInfo = await page.EvaluateAsync<string?>("() => window.__PLUGIN_STATE__?.apollo?.clientInfo ?? null");
        if (!string.IsNullOrWhiteSpace(clientInfo))
        {
            return clientInfo;
        }

        await page.GotoAsync(options.GetHomeUrl(), new PageGotoOptions
        {
            Timeout = (float)TimeSpan.FromSeconds(options.RequestTimeoutSeconds).TotalMilliseconds,
            WaitUntil = WaitUntilState.NetworkIdle
        });

        return await page.EvaluateAsync<string?>("() => window.__PLUGIN_STATE__?.apollo?.clientInfo ?? null");
    }

    private static ConcurrentDictionary<string, string?> BuildCookieMap(IEnumerable<BrowserContextCookiesResult> cookies)
    {
        var cookieMap = new ConcurrentDictionary<string, string?>();
        foreach (var cookieName in RequiredCookieNames)
        {
            var cookie = cookies.FirstOrDefault(x => x.Name == cookieName);
            cookieMap.TryAdd(cookieName, cookie?.Value);
        }

        return cookieMap;
    }

    private static string? GetCookieValue(IReadOnlyDictionary<string, string?> cookieMap, string key)
    {
        return cookieMap.TryGetValue(key, out var value) ? value : null;
    }

    private static string ExtractClientInfoToken(string? rawClientInfo, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawClientInfo))
        {
            return fallback;
        }

        var segments = rawClientInfo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 2)
        {
            return segments[1];
        }

        return segments.Length == 1 ? segments[0] : fallback;
    }

    private static string BuildHotelInfoUrlWithCurrentDates(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var uri = new Uri(url);
        var queryParameters = ParseQueryString(uri.Query);
        var checkIn = DateOnly.FromDateTime(DateTime.Today).AddDays(5);
        var checkOut = checkIn.AddDays(1);

        queryParameters["chkin"] = checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        queryParameters["chkout"] = checkOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var uriBuilder = new UriBuilder(uri)
        {
            Query = BuildQueryString(queryParameters)
        };

        return uriBuilder.Uri.ToString();
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return parameters;
        }

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            parameters[key] = value;
        }

        return parameters;
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string> queryParameters)
    {
        return string.Join("&", queryParameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }
}

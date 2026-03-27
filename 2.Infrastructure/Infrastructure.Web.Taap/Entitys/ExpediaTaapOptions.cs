using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapOptions
{
    public string BaseUrl { get; set; } = "https://www.expediataap.cn";

    public string GraphQlPath { get; set; } = "/graphql";

    public string LanguageId { get; set; } = "1033";

    public bool Headless { get; set; } = true;

    public int LoginCacheMinutes { get; set; } = 60;

    public int RequestTimeoutSeconds { get; set; } = 60;

    public int RequestsPerSecond { get; set; } = 1;

    public int RequestQueueLimit { get; set; } = 100;

    public string Locale { get; set; } = "en_US";

    public string Currency { get; set; } = "CNY";

    public int SiteId { get; set; } = 1070975;

    public int DefaultEapid { get; set; } = 70975;

    public string DefaultDuaid { get; set; } = "2461deff-a911-4e2a-91da-05ae4df02ff3";

    public string DefaultClientInfo { get; set; } = "66540f00d72e319b844c6b708d423bed9770fe31";

    public string Referrer { get; set; } = "HSR";

    public string DeviceType { get; set; } = "DESKTOP";

    public string PrivacyTrackingState { get; set; } = "CAN_TRACK";

    public string AuthState { get; set; } = "AUTHENTICATED";

    public string GraphQlRegion { get; set; } = "us-west-2";

    public bool ShowPackageRates { get; set; } = true;

    public string Sort { get; set; } = "RECOMMENDED";

    public string UseRewards { get; set; } = "SHOP_WITHOUT_POINTS";

    public string LoginStateCacheKey { get; set; } = "expedia-taap-login-state";

    public string? ProxyServer { get; set; }

    public string? BrowserChannel { get; set; }

    public string BrowserMode { get; set; } = "Remote";

    public string? RemoteBrowserWsEndpoint { get; set; }

    public string? RemoteBrowserCdpEndpoint { get; set; }

    public string GetHomeUrl() => $"{BaseUrl.TrimEnd('/')}/?langid={LanguageId}";

    public string GetLoginUrl() => $"{BaseUrl.TrimEnd('/')}/taap/signin?langid={LanguageId}";
}

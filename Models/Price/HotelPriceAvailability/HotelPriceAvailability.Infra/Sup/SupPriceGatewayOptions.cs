using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.Infra.Sup;

public sealed class SupPriceGatewayOptions
{
    public const string SectionName = "HotelPriceAvailability";

    public SupPriceEndpoints Endpoints { get; set; } = new();
    public SupPriceRetryOptions Retry { get; set; } = new();
}

public sealed class SupPriceEndpoints
{
    public string QueryPriceUrl { get; set; } = string.Empty;
    public string QueryCachePriceUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class SupPriceRetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMilliseconds { get; set; } = 500;
    public int TimeoutMilliseconds { get; set; } = 120000;
    public int CacheTimeoutMilliseconds { get; set; } = 30000;
}

public sealed class SupPriceGatewayOptionsValidator : IValidateOptions<SupPriceGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, SupPriceGatewayOptions options)
    {
        var errors = new List<string>();
        if (!Uri.TryCreate(options.Endpoints.QueryPriceUrl, UriKind.Absolute, out _))
        {
            errors.Add("Endpoints.QueryPriceUrl must be an absolute URI.");
        }

        if (!Uri.TryCreate(options.Endpoints.QueryCachePriceUrl, UriKind.Absolute, out _))
        {
            errors.Add("Endpoints.QueryCachePriceUrl must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoints.ApiKey))
        {
            errors.Add("Endpoints.ApiKey is required.");
        }

        if (options.Retry.MaxAttempts <= 0 || options.Retry.InitialDelayMilliseconds < 0 ||
            options.Retry.TimeoutMilliseconds <= 0 || options.Retry.CacheTimeoutMilliseconds <= 0)
        {
            errors.Add("Retry values are invalid.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

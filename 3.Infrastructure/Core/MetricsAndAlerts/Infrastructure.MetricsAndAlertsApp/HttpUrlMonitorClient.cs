using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MetricsAndAlertsApp;

namespace Infrastructure.MetricsAndAlertsApp;

public sealed class HttpUrlMonitorClient : IUrlMonitorClient
{
    const string HttpClientName = nameof(HttpUrlMonitorClient);
    static readonly TimeSpan CertificateExpiryAlertThreshold = TimeSpan.FromDays(7);
    readonly IHttpClientFactory _httpClientFactory;

    public HttpUrlMonitorClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<UrlMonitorCheckResult> CheckAsync(TargetUrlMonitorRule rule, CancellationToken cancellationToken = default)
    {
        var expectedStatusCodes = MetricsAndAlertsUtilities.ParseExpectedStatusCodes(rule.ExpectedStatusCodes);
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, rule.TimeoutSeconds)));
        CertificateInspectionResult? certificateInspection = null;

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(rule.HttpMethod), rule.Url);
            using var inspectionHandler = TryCreateInspectionHandler(rule.Url, out var inspectionResult);
            certificateInspection = inspectionResult;
            var client = inspectionHandler is null
                ? _httpClientFactory.CreateClient(HttpClientName)
                : new HttpClient(inspectionHandler, disposeHandler: false);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            stopwatch.Stop();

            var certificateError = GetCertificateErrorMessage(certificateInspection);
            if (!string.IsNullOrWhiteSpace(certificateError))
            {
                return new UrlMonitorCheckResult
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    ErrorMessage = certificateError
                };
            }

            var statusCode = (int)response.StatusCode;
            var isSuccess = expectedStatusCodes.Contains(statusCode);
            return new UrlMonitorCheckResult
            {
                IsSuccess = isSuccess,
                StatusCode = statusCode,
                DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                ErrorMessage = isSuccess ? null : $"HTTP status code {statusCode} not in expected set {rule.ExpectedStatusCodes}."
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new UrlMonitorCheckResult
            {
                IsSuccess = false,
                StatusCode = null,
                DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                ErrorMessage = $"Request timeout after {rule.TimeoutSeconds} seconds: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new UrlMonitorCheckResult
            {
                IsSuccess = false,
                StatusCode = null,
                DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                ErrorMessage = BuildExceptionMessage(ex, certificateInspection)
            };
        }
    }

    static HttpClientHandler? TryCreateInspectionHandler(string url, out CertificateInspectionResult? inspectionResult)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            inspectionResult = null;
            return null;
        }

        var capturedInspectionResult = new CertificateInspectionResult();
        inspectionResult = capturedInspectionResult;
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, chain, sslPolicyErrors) =>
            {
                capturedInspectionResult.Capture(certificate, chain, sslPolicyErrors);
                return true;
            }
        };
    }

    static string BuildExceptionMessage(Exception exception, CertificateInspectionResult? certificateInspection)
    {
        var certificateError = GetCertificateErrorMessage(certificateInspection);
        return string.IsNullOrWhiteSpace(certificateError)
            ? exception.Message
            : $"{certificateError} HTTP request failed: {exception.Message}";
    }

    static string? GetCertificateErrorMessage(CertificateInspectionResult? inspection)
    {
        if (inspection is null)
        {
            return null;
        }

        if (!inspection.CallbackInvoked)
        {
            return "HTTPS certificate inspection did not complete.";
        }

        if (!inspection.HasCertificate)
        {
            return "HTTPS certificate was not provided by the remote server.";
        }

        if (inspection.SslPolicyErrors != SslPolicyErrors.None)
        {
            return $"HTTPS certificate is invalid: {inspection.DescribePolicyErrors()}";
        }

        if (inspection.ExpiresIn <= CertificateExpiryAlertThreshold)
        {
            var expiresAt = inspection.NotAfterUtc?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            var remainingDays = Math.Floor(Math.Max(inspection.ExpiresIn.TotalDays, 0));
            return $"HTTPS certificate expires soon: expires at {expiresAt}, remaining about {remainingDays} days.";
        }

        return null;
    }

    sealed class CertificateInspectionResult
    {
        public bool CallbackInvoked { get; private set; }
        public bool HasCertificate { get; private set; }
        public SslPolicyErrors SslPolicyErrors { get; private set; } = SslPolicyErrors.None;
        public DateTimeOffset? NotAfterUtc { get; private set; }
        public TimeSpan ExpiresIn { get; private set; } = TimeSpan.MaxValue;
        public IReadOnlyList<string> ChainErrors { get; private set; } = Array.Empty<string>();

        public void Capture(X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            CallbackInvoked = true;
            SslPolicyErrors = sslPolicyErrors;
            HasCertificate = certificate is not null;

            if (certificate is null)
            {
                ChainErrors = Array.Empty<string>();
                NotAfterUtc = null;
                ExpiresIn = TimeSpan.Zero;
                return;
            }

            NotAfterUtc = certificate.NotAfter.ToUniversalTime();
            ExpiresIn = NotAfterUtc.Value - DateTimeOffset.UtcNow;
            ChainErrors = chain?.ChainStatus
                .Where(status => status.Status != X509ChainStatusFlags.NoError)
                .Select(status => string.IsNullOrWhiteSpace(status.StatusInformation)
                    ? status.Status.ToString()
                    : $"{status.Status}: {status.StatusInformation.Trim()}")
                .Distinct(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();
        }

        public string DescribePolicyErrors()
        {
            var parts = new List<string> { SslPolicyErrors.ToString() };
            if (ChainErrors.Count > 0)
            {
                parts.AddRange(ChainErrors);
            }

            return string.Join("; ", parts.Distinct(StringComparer.Ordinal));
        }
    }
}

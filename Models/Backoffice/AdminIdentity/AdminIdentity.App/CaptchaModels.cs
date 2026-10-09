namespace AdminIdentity.App;

public static class DeviceCookieName
{
    public const string Value = "MengtuV.Admin.DeviceId";
}

public sealed record CaptchaRequiredResponse(bool Required);

public sealed record CaptchaVerifyRequest(string UserName, string Token);

public sealed record CaptchaVerifyResponse(string CaptchaToken, int ExpiresInSeconds);

public sealed record ImageCaptchaResponse(string CaptchaId, string ImageBase64);

public sealed record ImageCaptchaVerifyRequest(string UserName, string CaptchaId, string Answer);

public interface ICaptchaService
{
    Task<bool> IsCaptchaRequiredAsync(Guid? userId, string? deviceId, CancellationToken cancellationToken);

    Task<bool> IsCaptchaRequiredByUserAsync(string? userName, string? deviceId, CancellationToken cancellationToken);

    Task<int> GetFailureCountAsync(string userName, CancellationToken cancellationToken);

    Task IncrementFailureAsync(string userName, CancellationToken cancellationToken);

    Task ResetFailureAsync(string userName, CancellationToken cancellationToken);

    Task<string> IssueTokenAsync(string deviceId, string userName, CancellationToken cancellationToken);

    Task<string> IssueTokenWithDeviceAsync(string? existingDeviceId, string userName,
        Action<string> setDeviceCookie, CancellationToken cancellationToken);

    Task<bool> ConsumeTokenAsync(string deviceId, string userName, string captchaToken, CancellationToken cancellationToken);

    Task<ImageCaptchaResponse> GenerateImageCaptchaAsync(CancellationToken cancellationToken);

    Task<bool> ValidateImageCaptchaAsync(string captchaId, string answer, CancellationToken cancellationToken);
}

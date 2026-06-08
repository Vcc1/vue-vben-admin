using Microsoft.Extensions.Options;

namespace GrafanaAlertApp;

public sealed class GrafanaAlertOptions
{
    public string[] DefaultToUsers { get; init; } = ["zhangwei", "tangwei"];

    public int AgentId { get; init; } = 1000026;
}

public sealed class GrafanaAlertOptionsValidator : IValidateOptions<GrafanaAlertOptions>
{
    public ValidateOptionsResult Validate(string? name, GrafanaAlertOptions options)
    {
        if (options.DefaultToUsers.Length == 0 ||
            options.DefaultToUsers.All(static x => string.IsNullOrWhiteSpace(x)))
        {
            return ValidateOptionsResult.Fail("GrafanaAlert 缺少默认通知人。");
        }

        if (options.AgentId <= 0)
        {
            return ValidateOptionsResult.Fail("GrafanaAlert AgentId 必须大于 0。");
        }

        return ValidateOptionsResult.Success;
    }
}

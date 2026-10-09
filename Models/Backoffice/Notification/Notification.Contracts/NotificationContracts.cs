namespace Notification.Contracts;

public sealed record NotificationDraft(
    string TypeCode,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    NotificationTarget Target,
    string? SourceEventKey = null);

public sealed record NotificationTarget(
    string Scope,
    IReadOnlyList<string> TargetIds);

public static class NotificationScopes
{
    public const string All = "all";
    public const string Roles = "roles";
    public const string Users = "users";
}

public static class NotificationSources
{
    public const string System = "system";
    public const string Manual = "manual";
}

public static class NotificationChannels
{
    public const string InApp = "in_app";
    public const string WeChat = "wechat";

    public static IReadOnlyList<string> All { get; } = [InApp, WeChat];
}

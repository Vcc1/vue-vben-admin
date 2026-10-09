namespace Notification.App;

public sealed record NotificationDto(
    Guid Id,
    long RecipientId,
    string TypeCode,
    string TypeDisplayName,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedAtUtc);

public sealed record NotificationTypeDto(
    Guid Id,
    string TypeCode,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> AllowedChannels,
    bool IsEnabled,
    bool IsSystem,
    string RowVersion);

public sealed record NotificationTypeInput(
    string TypeCode,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> AllowedChannels,
    bool IsEnabled);

public sealed record UserNotificationPreferenceDto(
    string TypeCode,
    string TypeDisplayName,
    IReadOnlyList<string> AllowedChannels,
    IReadOnlyList<string> EnabledChannels,
    bool IsEnabled);

public sealed record UserNotificationPreferenceInput(
    string TypeCode,
    IReadOnlyList<string> EnabledChannels);

public sealed record PublishNotificationInput(
    string TypeCode,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    string Scope,
    IReadOnlyList<string> TargetIds);

public sealed record NotificationTypeRecord(
    Guid Id,
    string TypeCode,
    string DisplayName,
    string? Description,
    string AllowedChannelsJson,
    bool IsEnabled,
    bool IsSystem,
    string RowVersion);

public sealed record NotificationPreferenceRecord(
    Guid Id,
    Guid NotificationTypeId,
    string EnabledChannelsJson);

public sealed record NotificationBrief(
    Guid Id,
    string TypeCode,
    string TypeDisplayName,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    DateTime CreatedAtUtc);

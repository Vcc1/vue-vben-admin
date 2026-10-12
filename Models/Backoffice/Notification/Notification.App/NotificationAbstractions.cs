namespace Notification.App;

public interface INotificationStore
{
    Task<IReadOnlyList<NotificationTypeRecord>> ListTypesAsync(CancellationToken ct);
    Task<NotificationTypeRecord?> GetTypeByCodeAsync(string typeCode, CancellationToken ct);
    Task<NotificationTypeRecord?> GetTypeByIdAsync(Guid typeId, CancellationToken ct);
    Task<NotificationTypeRecord> CreateTypeAsync(NotificationTypeInput input, string actorId, CancellationToken ct);
    Task<NotificationTypeRecord> UpdateTypeAsync(Guid typeId, NotificationTypeInput input, string rowVersion, string actorId, CancellationToken ct);
    Task DeleteTypeAsync(Guid typeId, CancellationToken ct);

    Task<NotificationBrief> SaveNotificationAsync(NotificationPublishRequest request, CancellationToken ct);
    Task UpdateRecipientDeliveredChannelsAsync(long recipientId, string deliveredChannelsJson, CancellationToken ct);

    Task<UserNotificationPage> GetUserNotificationsAsync(Guid userId, NotificationQuery query, CancellationToken ct);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct);
    Task MarkAsReadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken ct);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<NotificationPreferenceRecord>> GetUserPreferencesAsync(Guid userId, CancellationToken ct);
    Task SaveUserPreferenceAsync(Guid userId, Guid notificationTypeId, string enabledChannelsJson, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ResolveTargetUserIdsAsync(string scope, IReadOnlyList<string> targetIds, CancellationToken ct);
}

public sealed record NotificationPublishRequest(
    Guid NotificationTypeId,
    string TypeCode,
    string TypeDisplayName,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    string Scope,
    string Source,
    string? SourceEventKey,
    string CreatedByActorId,
    string CreatedByActorName,
    IReadOnlyList<string> TargetIds,
    IReadOnlyList<Guid> UserIds);

public interface INotificationChannelHandler
{
    string ChannelCode { get; }
    Task SendAsync(NotificationBrief notification, IReadOnlyList<Guid> userIds, CancellationToken ct);
}

public interface INotificationHubPusher
{
    Task PushToUsersAsync(IReadOnlyList<Guid> userIds, NotificationDto dto, CancellationToken ct);
}

public sealed record NotificationQuery(
    string? TypeCode,
    bool? IsRead,
    int Skip,
    int Take);

public sealed record UserNotificationPage(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount);

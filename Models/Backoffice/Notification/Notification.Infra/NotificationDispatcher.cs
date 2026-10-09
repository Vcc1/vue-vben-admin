using System.Text.Json;
using AdminIdentity.App;
using Microsoft.Extensions.Logging;
using Notification.App;
using Notification.Contracts;

namespace Notification.Infra;

internal sealed class NotificationDispatcher(
    INotificationStore store,
    IEnumerable<INotificationChannelHandler> channelHandlers,
    ICurrentActorAccessor actorAccessor,
    ILogger<NotificationDispatcher> logger) : INotificationPublisher
{
    public async Task PublishAsync(NotificationDraft draft, string? source = null, CancellationToken ct = default)
    {
        var effectiveSource = source ?? NotificationSources.System;
        var type = await store.GetTypeByCodeAsync(draft.TypeCode, ct);
        if (type is null)
        {
            logger.LogWarning("Notification type {TypeCode} not found", draft.TypeCode);
            return;
        }
        if (!type.IsEnabled)
        {
            logger.LogInformation("Notification type {TypeCode} is disabled, skipping", draft.TypeCode);
            return;
        }

        var allowedChannels = JsonSerializer.Deserialize<List<string>>(type.AllowedChannelsJson) ?? [];
        var userIds = await store.ResolveTargetUserIdsAsync(draft.Target.Scope, draft.Target.TargetIds, ct);
        if (userIds.Count == 0)
        {
            logger.LogInformation("No target users resolved for notification {TypeCode}", draft.TypeCode);
            return;
        }

        var request = new NotificationPublishRequest(
            type.Id,
            type.TypeCode,
            type.DisplayName,
            draft.Title,
            draft.Summary,
            draft.ContentJson,
            draft.LinkUrl,
            draft.Target.Scope,
            effectiveSource,
            draft.SourceEventKey,
            actorAccessor.Current.ActorId,
            actorAccessor.Current.ActorName,
            draft.Target.TargetIds,
            userIds);

        var brief = await store.SaveNotificationAsync(request, ct);

        foreach (var channelCode in allowedChannels)
        {
            var handler = channelHandlers.FirstOrDefault(h => h.ChannelCode == channelCode);
            if (handler is null) continue;
            try
            {
                await handler.SendAsync(brief, userIds, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send notification {NotificationId} via channel {Channel}",
                    brief.Id, channelCode);
            }
        }
    }
}

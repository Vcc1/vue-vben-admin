using System.Text.Json;
using AdminIdentity.App;

namespace Notification.App;

public sealed class NotificationService(
    INotificationStore store,
    ICurrentActorAccessor actorAccessor)
{
    public async Task<IReadOnlyList<NotificationTypeDto>> ListTypesAsync(CancellationToken ct)
    {
        var types = await store.ListTypesAsync(ct);
        return types.Select(MapType).ToList();
    }

    public async Task<NotificationTypeDto> CreateTypeAsync(NotificationTypeInput input, CancellationToken ct)
    {
        var created = await store.CreateTypeAsync(input, actorAccessor.Current.ActorId, ct);
        return MapType(created);
    }

    public async Task<NotificationTypeDto> UpdateTypeAsync(Guid typeId, NotificationTypeInput input, string rowVersion, CancellationToken ct)
    {
        var updated = await store.UpdateTypeAsync(typeId, input, rowVersion, actorAccessor.Current.ActorId, ct);
        return MapType(updated);
    }

    public async Task DeleteTypeAsync(Guid typeId, CancellationToken ct)
    {
        await store.DeleteTypeAsync(typeId, ct);
    }

    private static NotificationTypeDto MapType(NotificationTypeRecord r)
    {
        var channels = JsonSerializer.Deserialize<List<string>>(r.AllowedChannelsJson) ?? [];
        return new NotificationTypeDto(
            r.Id, r.TypeCode, r.DisplayName, r.Description,
            channels, r.IsEnabled, r.IsSystem, r.RowVersion);
    }
}

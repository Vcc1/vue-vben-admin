using System.Text.Json;

namespace Notification.App;

public sealed class NotificationPreferenceService(
    INotificationStore store,
    ICurrentUserIdAccessor userIdAccessor)
{
    public async Task<IReadOnlyList<UserNotificationPreferenceDto>> GetPreferencesAsync(CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        var types = await store.ListTypesAsync(ct);
        var prefs = await store.GetUserPreferencesAsync(userId, ct);
        var prefDict = prefs.ToDictionary(p => p.NotificationTypeId);

        return types.Select(t =>
        {
            var allowed = JsonSerializer.Deserialize<List<string>>(t.AllowedChannelsJson) ?? [];
            if (prefDict.TryGetValue(t.Id, out var pref))
            {
                var enabled = JsonSerializer.Deserialize<List<string>>(pref.EnabledChannelsJson) ?? [];
                return new UserNotificationPreferenceDto(
                    t.TypeCode, t.DisplayName, allowed, enabled, t.IsEnabled);
            }
            return new UserNotificationPreferenceDto(
                t.TypeCode, t.DisplayName, allowed, allowed, t.IsEnabled);
        }).ToList();
    }

    public async Task UpdatePreferencesAsync(IReadOnlyList<UserNotificationPreferenceInput> inputs, CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        var types = await store.ListTypesAsync(ct);
        var typeDict = types.ToDictionary(t => t.TypeCode);

        foreach (var input in inputs)
        {
            if (!typeDict.TryGetValue(input.TypeCode, out var type)) continue;
            var allowed = JsonSerializer.Deserialize<List<string>>(type.AllowedChannelsJson) ?? [];
            var validChannels = input.EnabledChannels.Where(c => allowed.Contains(c)).ToList();
            var json = JsonSerializer.Serialize(validChannels);
            await store.SaveUserPreferenceAsync(userId, type.Id, json, ct);
        }
    }
}

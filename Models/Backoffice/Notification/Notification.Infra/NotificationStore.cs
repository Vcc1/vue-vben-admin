using System.Text.Json;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Notification;
using Microsoft.EntityFrameworkCore;
using Notification.App;

namespace Notification.Infra;

internal sealed class NotificationStore(AdminDbContext db) : INotificationStore
{
    public async Task<IReadOnlyList<NotificationTypeRecord>> ListTypesAsync(CancellationToken ct) =>
        await db.NotificationTypes
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.DisplayName)
            .Select(t => ToTypeRecord(t))
            .ToListAsync(ct);

    public async Task<NotificationTypeRecord?> GetTypeByCodeAsync(string typeCode, CancellationToken ct) =>
        await db.NotificationTypes
            .Where(t => t.TypeCode == typeCode && !t.IsDeleted)
            .Select(t => ToTypeRecord(t))
            .FirstOrDefaultAsync(ct);

    public async Task<NotificationTypeRecord?> GetTypeByIdAsync(Guid typeId, CancellationToken ct) =>
        await db.NotificationTypes
            .Where(t => t.Id == typeId && !t.IsDeleted)
            .Select(t => ToTypeRecord(t))
            .FirstOrDefaultAsync(ct);

    public async Task<NotificationTypeRecord> CreateTypeAsync(NotificationTypeInput input, string actorId, CancellationToken ct)
    {
        var entity = new NotificationTypeEntity
        {
            Id = Guid.CreateVersion7(),
            TypeCode = input.TypeCode.Trim().ToLowerInvariant(),
            DisplayName = input.DisplayName.Trim(),
            Description = input.Description?.Trim(),
            AllowedChannelsJson = JsonSerializer.Serialize(input.AllowedChannels),
            IsEnabled = input.IsEnabled,
            IsSystem = false,
            CreatedByActorId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedByActorId = actorId,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.NotificationTypes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToTypeRecord(entity);
    }

    public async Task<NotificationTypeRecord> UpdateTypeAsync(Guid typeId, NotificationTypeInput input, string rowVersion, string actorId, CancellationToken ct)
    {
        var entity = await db.NotificationTypes.FirstAsync(t => t.Id == typeId && !t.IsDeleted, ct);
        entity.TypeCode = input.TypeCode.Trim().ToLowerInvariant();
        entity.DisplayName = input.DisplayName.Trim();
        entity.Description = input.Description?.Trim();
        entity.AllowedChannelsJson = JsonSerializer.Serialize(input.AllowedChannels);
        entity.IsEnabled = input.IsEnabled;
        entity.UpdatedByActorId = actorId;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToTypeRecord(entity);
    }

    public async Task DeleteTypeAsync(Guid typeId, CancellationToken ct)
    {
        var entity = await db.NotificationTypes.FirstOrDefaultAsync(t => t.Id == typeId, ct);
        if (entity is null) return;
        if (entity.IsSystem) throw new InvalidOperationException("Cannot delete system notification type");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationBrief> SaveNotificationAsync(NotificationPublishRequest request, CancellationToken ct)
    {
        var notification = new NotificationEntity
        {
            Id = Guid.CreateVersion7(),
            NotificationTypeId = request.NotificationTypeId,
            Title = request.Title,
            Summary = request.Summary,
            ContentJson = request.ContentJson,
            LinkUrl = request.LinkUrl,
            Scope = request.Scope,
            TargetIdsJson = JsonSerializer.Serialize(request.TargetIds),
            Source = request.Source,
            SourceEventKey = request.SourceEventKey,
            CreatedByActorId = request.CreatedByActorId,
            CreatedByActorName = request.CreatedByActorName,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var recipients = request.UserIds.Select(uid => new NotificationRecipientEntity
        {
            NotificationId = notification.Id,
            UserId = uid,
            CreatedAtUtc = DateTime.UtcNow,
        }).ToList();

        db.Notifications.Add(notification);
        db.NotificationRecipients.AddRange(recipients);
        await db.SaveChangesAsync(ct);

        return new NotificationBrief(
            notification.Id,
            request.TypeCode,
            request.TypeDisplayName,
            notification.Title,
            notification.Summary,
            notification.ContentJson,
            notification.LinkUrl,
            notification.CreatedAtUtc);
    }

    public async Task UpdateRecipientDeliveredChannelsAsync(long recipientId, string deliveredChannelsJson, CancellationToken ct)
    {
        var recipient = await db.NotificationRecipients.FirstOrDefaultAsync(r => r.Id == recipientId, ct);
        if (recipient is not null)
        {
            recipient.DeliveredChannelsJson = deliveredChannelsJson;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserNotificationPage> GetUserNotificationsAsync(Guid userId, NotificationQuery query, CancellationToken ct)
    {
        var q = db.NotificationRecipients
            .Include(r => r.Notification)
            .ThenInclude(n => n.NotificationType)
            .Where(r => r.UserId == userId);

        if (query.TypeCode is not null)
            q = q.Where(r => r.Notification.NotificationType.TypeCode == query.TypeCode);
        if (query.IsRead.HasValue)
            q = q.Where(r => r.IsRead == query.IsRead.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(r => new NotificationDto(
                r.NotificationId,
                r.Id,
                r.Notification.NotificationType.TypeCode,
                r.Notification.NotificationType.DisplayName,
                r.Notification.Title,
                r.Notification.Summary,
                r.Notification.ContentJson,
                r.Notification.LinkUrl,
                r.IsRead,
                r.CreatedAtUtc))
            .ToListAsync(ct);

        return new UserNotificationPage(items, total);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct) =>
        await db.NotificationRecipients.CountAsync(r => r.UserId == userId && !r.IsRead, ct);

    public async Task MarkAsReadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken ct)
    {
        var recipients = await db.NotificationRecipients
            .Where(r => r.UserId == userId && notificationIds.Contains(r.NotificationId) && !r.IsRead)
            .ToListAsync(ct);
        foreach (var r in recipients)
        {
            r.IsRead = true;
            r.ReadAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct)
    {
        var recipients = await db.NotificationRecipients
            .Where(r => r.UserId == userId && !r.IsRead)
            .ToListAsync(ct);
        foreach (var r in recipients)
        {
            r.IsRead = true;
            r.ReadAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationPreferenceRecord>> GetUserPreferencesAsync(Guid userId, CancellationToken ct) =>
        await db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .Select(p => new NotificationPreferenceRecord(p.Id, p.NotificationTypeId, p.EnabledChannelsJson))
            .ToListAsync(ct);

    public async Task SaveUserPreferenceAsync(Guid userId, Guid notificationTypeId, string enabledChannelsJson, CancellationToken ct)
    {
        var existing = await db.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationTypeId == notificationTypeId, ct);
        if (existing is not null)
        {
            existing.EnabledChannelsJson = enabledChannelsJson;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreferenceEntity
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                NotificationTypeId = notificationTypeId,
                EnabledChannelsJson = enabledChannelsJson,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ResolveTargetUserIdsAsync(string scope, IReadOnlyList<string> targetIds, CancellationToken ct)
    {
        return scope switch
        {
            Contracts.NotificationScopes.All => await db.Users
                .Where(u => u.IsEnabled)
                .Select(u => u.Id)
                .ToListAsync(ct),
            Contracts.NotificationScopes.Roles => await db.UserRoles
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.NormalizedName })
                .Where(x => targetIds.Contains(x.NormalizedName ?? ""))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(ct),
            Contracts.NotificationScopes.Users => targetIds.Select(Guid.Parse).ToList(),
            _ => []
        };
    }

    private static NotificationTypeRecord ToTypeRecord(NotificationTypeEntity e) =>
        new(e.Id, e.TypeCode, e.DisplayName, e.Description,
            e.AllowedChannelsJson, e.IsEnabled, e.IsSystem,
            Convert.ToBase64String(e.RowVersion));
}

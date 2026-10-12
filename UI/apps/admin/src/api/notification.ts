import { baseRequestClient } from '#/api/request';

export interface NotificationDto {
  contentJson: string;
  createdAtUtc: string;
  id: string;
  isRead: boolean;
  linkUrl: string | null;
  recipientId: number;
  summary: string;
  title: string;
  typeCode: string;
  typeDisplayName: string;
}

export interface NotificationPage {
  items: NotificationDto[];
  totalCount: number;
}

export interface NotificationTypeDto {
  allowedChannels: string[];
  description: string | null;
  displayName: string;
  id: string;
  isEnabled: boolean;
  isSystem: boolean;
  rowVersion: string;
  typeCode: string;
}

export interface NotificationTypeInput {
  allowedChannels: string[];
  description: string | null;
  displayName: string;
  isEnabled: boolean;
  typeCode: string;
}

export interface UserNotificationPreferenceDto {
  allowedChannels: string[];
  enabledChannels: string[];
  isEnabled: boolean;
  typeCode: string;
  typeDisplayName: string;
}

export interface UserNotificationPreferenceInput {
  enabledChannels: string[];
  typeCode: string;
}

export interface PublishNotificationInput {
  contentJson: string;
  linkUrl: string | null;
  scope: string;
  summary: string;
  targetIds: string[];
  title: string;
  typeCode: string;
}

export async function getMyNotificationsApi(params: {
  isRead?: boolean;
  skip?: number;
  take?: number;
  typeCode?: string;
}) {
  return baseRequestClient.get<NotificationPage>('/admin/notifications', {
    params,
  });
}

export async function getUnreadCountApi() {
  return baseRequestClient.get<number>('/admin/notifications/unread-count');
}

export async function markAsReadApi(ids: string[]) {
  return baseRequestClient.post('/admin/notifications/mark-read', { ids });
}

export async function markAllAsReadApi() {
  return baseRequestClient.post('/admin/notifications/mark-all-read');
}

export async function getNotificationTypesApi() {
  return baseRequestClient.get<NotificationTypeDto[]>(
    '/admin/notification-types',
  );
}

export async function createNotificationTypeApi(data: NotificationTypeInput) {
  return baseRequestClient.post<NotificationTypeDto>(
    '/admin/notification-types',
    data,
  );
}

export async function updateNotificationTypeApi(
  id: string,
  data: NotificationTypeInput,
) {
  return baseRequestClient.put<NotificationTypeDto>(
    `/admin/notification-types/${id}`,
    data,
  );
}

export async function deleteNotificationTypeApi(id: string) {
  return baseRequestClient.delete(`/admin/notification-types/${id}`);
}

export async function getMyPreferencesApi() {
  return baseRequestClient.get<UserNotificationPreferenceDto[]>(
    '/admin/notifications/preferences',
  );
}

export async function updateMyPreferencesApi(
  preferences: UserNotificationPreferenceInput[],
) {
  return baseRequestClient.put('/admin/notifications/preferences', {
    preferences,
  });
}

export async function publishNotificationApi(data: PublishNotificationInput) {
  return baseRequestClient.post('/admin/notifications/publish', data);
}

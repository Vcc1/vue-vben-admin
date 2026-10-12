import { baseRequestClient } from '#/api/request';

export interface ApiKeyItem {
  createdAtUtc: string;
  expiresAtUtc: string | null;
  id: string;
  lastUsedAtUtc: string | null;
  name: string;
  prefix: string;
  revokedAtUtc: string | null;
  userId: string;
}

export interface CreatedApiKey {
  expiresAtUtc: string | null;
  id: string;
  name: string;
  prefix: string;
  secret: string;
}

export interface CreateMyApiKeyRequest {
  expiresAtUtc: string | null;
  name: string;
}

export async function listMyApiKeysApi() {
  return baseRequestClient.get<ApiKeyItem[]>('/admin/my/api-keys');
}

export async function createMyApiKeyApi(data: CreateMyApiKeyRequest) {
  return baseRequestClient.post<CreatedApiKey>('/admin/my/api-keys', data);
}

export async function revokeMyApiKeyApi(apiKeyId: string) {
  return baseRequestClient.delete(`/admin/my/api-keys/${apiKeyId}`);
}

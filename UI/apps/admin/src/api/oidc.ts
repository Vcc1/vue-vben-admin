import { baseRequestClient } from '#/api/request';

export interface OidcClientItem {
  clientId: string;
  consentType: string;
  createdAtUtc: string;
  displayName: string;
  grantTypes: string[];
  hasClientSecret: boolean;
  id: string;
  postLogoutRedirectUris: string[];
  redirectUris: string[];
  scopes: string[];
  clientType: string;
}

export interface CreateOidcClientRequest {
  clientId: string;
  clientSecret?: string;
  clientType: string;
  consentType: string;
  displayName: string;
  grantTypes: string[];
  postLogoutRedirectUris: string[];
  redirectUris: string[];
  scopes: string[];
}

export interface UpdateOidcClientRequest {
  clientType?: string;
  consentType: string;
  displayName: string;
  grantTypes: string[];
  postLogoutRedirectUris: string[];
  redirectUris: string[];
  scopes: string[];
}

export interface OidcScopeItem {
  description: string;
  displayName: string;
  id: string;
  name: string;
}

export interface CreateOidcScopeRequest {
  description: string;
  displayName: string;
  name: string;
}

export async function listOidcClientsApi() {
  return baseRequestClient.get<OidcClientItem[]>('/admin/oidc/clients');
}

export async function getOidcClientApi(id: string) {
  return baseRequestClient.get<OidcClientItem>(`/admin/oidc/clients/${id}`);
}

export async function createOidcClientApi(data: CreateOidcClientRequest) {
  return baseRequestClient.post<OidcClientItem>('/admin/oidc/clients', data);
}

export async function updateOidcClientApi(
  id: string,
  data: UpdateOidcClientRequest,
) {
  return baseRequestClient.put<OidcClientItem>(
    `/admin/oidc/clients/${id}`,
    data,
  );
}

export async function deleteOidcClientApi(id: string) {
  return baseRequestClient.delete(`/admin/oidc/clients/${id}`);
}

export async function regenerateOidcClientSecretApi(id: string) {
  return baseRequestClient.post<{ secret: string }>(
    `/admin/oidc/clients/${id}/regenerate-secret`,
  );
}

export async function listOidcScopesApi() {
  return baseRequestClient.get<OidcScopeItem[]>('/admin/oidc/scopes');
}

export async function createOidcScopeApi(data: CreateOidcScopeRequest) {
  return baseRequestClient.post<OidcScopeItem>('/admin/oidc/scopes', data);
}

export async function deleteOidcScopeApi(id: string) {
  return baseRequestClient.delete(`/admin/oidc/scopes/${id}`);
}

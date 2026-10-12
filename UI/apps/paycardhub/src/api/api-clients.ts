import type { PagedResponse } from './types';

import { adminClient } from './admin-client';

export interface ApiClientListItem {
  cardCount: number;
  clientCode: string;
  createdAt: string;
  displayName: string;
  id: number;
  isActive: boolean;
  apiKeyCount: number;
  orderCount: number;
}

export interface ApiClientListParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export function getClientsApi(params: ApiClientListParams) {
  return adminClient.get<PagedResponse<ApiClientListItem>>('/api-clients', { params });
}

export interface ApiKeyMetadata {
  allowedIpAddresses: string;
  createdAt: string;
  id: number;
  isActive: boolean;
  keyName: string;
}

export interface ApiClientDetail {
  cardCount: number;
  clientCode: string;
  createdAt: string;
  displayName: string;
  id: number;
  isActive: boolean;
  apiKeys: ApiKeyMetadata[];
  orderCount: number;
  transactionCount: number;
}

export function getClientDetailApi(id: number) {
  return adminClient.get<ApiClientDetail>(`/api-clients/${id}`);
}

export interface ApiClientOption {
  clientCode: string;
  displayName: string;
  id: number;
  isActive: boolean;
}

export function getClientOptionsApi() {
  return adminClient.get<ApiClientOption[]>('/api-client-options');
}

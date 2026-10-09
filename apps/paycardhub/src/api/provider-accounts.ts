import type { PagedResponse } from './types';

import { adminClient } from './admin-client';

export interface ProviderAccountListItem {
  accountName: string;
  autoIssueCurrenciesJson?: string;
  autoIssueEnabled: boolean;
  autoIssueMaxDays?: number;
  autoIssuePriority: number;
  cardCount: number;
  cardholderMappingCount: number;
  createdAt: string;
  id: number;
  isActive: boolean;
  lastSuccessfulSyncedAt?: string;
  providerCode: string;
  transactionCount: number;
}

export interface ProviderAccountListParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerCode?: string;
}

export function getProviderAccountsApi(params: ProviderAccountListParams) {
  return adminClient.get<PagedResponse<ProviderAccountListItem>>('/card-provider-accounts', {
    params,
  });
}

export interface ProviderConfigurationStatus {
  hasApiKey: boolean;
  hasBaseUrl: boolean;
  hasCardholderId: boolean;
  hasClientId: boolean;
}

export interface ProviderAccountDetail {
  accountName: string;
  autoIssueCurrenciesJson?: string;
  autoIssueEnabled: boolean;
  autoIssueMaxDays?: number;
  autoIssuePriority: number;
  cardCount: number;
  cardholderMappingCount: number;
  configuration: ProviderConfigurationStatus;
  createdAt: string;
  id: number;
  isActive: boolean;
  lastSuccessfulSyncedAt?: string;
  orderCount: number;
  providerCode: string;
  rawConfiguration?: Record<string, string | null>;
  transactionCount: number;
}

export function getProviderAccountDetailApi(id: number) {
  return adminClient.get<ProviderAccountDetail>(`/card-provider-accounts/${id}`);
}

export interface UpsertProviderAccountRequest {
  accountName: string;
  autoIssueCurrenciesJson?: string;
  autoIssueEnabled: boolean;
  autoIssueMaxDays?: number;
  autoIssuePriority: number;
  configuration: Record<string, string | null>;
  isActive: boolean;
  providerCode: string;
}

export function createProviderAccountApi(data: UpsertProviderAccountRequest) {
  return adminClient.post<ProviderAccountDetail>('/card-provider-accounts', data);
}

export function updateProviderAccountApi(id: number, data: UpsertProviderAccountRequest) {
  return adminClient.put<ProviderAccountDetail>(`/card-provider-accounts/${id}`, data);
}

export function getProviderAccountConfigurationApi(id: number) {
  return adminClient.get<Record<string, string | null>>(`/card-provider-accounts/${id}/configuration`);
}

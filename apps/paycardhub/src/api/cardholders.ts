import type { PagedResponse } from './types';

import { adminClient } from './admin-client';

export interface CardholderListItem {
  cardCount: number;
  createdAt: string;
  displayName: string;
  email: string;
  id: number;
  mobileNumber: string;
  nationality: string;
  providerMappingCount: number;
  status: string;
  type: string;
}

export interface CardholderListParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export function getCardholdersApi(params: CardholderListParams) {
  return adminClient.get<PagedResponse<CardholderListItem>>('/cardholders', { params });
}

export interface ProviderCardholderMapping {
  id: number;
  providerAccountId: number;
  providerAccountName: string;
  providerCardholderId: string;
  providerCode: string;
  status: string;
  updatedAt: string;
}

export interface RelatedCard {
  expireAt: string;
  id: number;
  maskedCardNumber: string;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  status: string;
  apiClientCode: string;
}

export interface CardholderDetail {
  cardCount: number;
  cards: RelatedCard[];
  city: string;
  country: string;
  createdAt: string;
  dateOfBirth: string;
  displayName: string;
  email: string;
  id: number;
  mobileNumber: string;
  nationality: string;
  providerMappingCount: number;
  providerMappings: ProviderCardholderMapping[];
  status: string;
  type: string;
  updatedAt: string;
}

export function getCardholderDetailApi(id: number) {
  return adminClient.get<CardholderDetail>(`/cardholders/${id}`);
}

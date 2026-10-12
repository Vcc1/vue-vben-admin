import type { PagedResponse } from './types';

import { adminClient } from './admin-client';

export interface CardUsageAmount {
  billingAmount: number;
  currency: string;
  includesUnprojectedFacts: boolean;
  isLimitCurrency: boolean;
  lastUpdatedAt: string;
  pendingAmount: number;
  settledAmount: number;
  totalAuthorized: number;
  totalDeclined: number;
  totalExpired: number;
  totalReversed: number;
  usedAmount: number;
}

export interface HotelOrder {
  checkIn?: string;
  checkOut?: string;
  mtHotelCode?: string;
  mtOrderNo?: string;
  sHotelCode?: string;
  supId?: number;
}

export interface CardListItem {
  apiClientCode: string;
  apiClientName?: string;
  businessOrderNo: string;
  cardholderId: number;
  cardholderName?: string;
  createdAt: string;
  expectedBusinessStatus: number;
  expireAt?: string;
  hotelOrder?: HotelOrder;
  id: number;
  latestObservedAt?: string;
  latestTransactionAt?: string;
  limitAmountCurrency: string;
  limitAmountValue: number;
  maskedCardNumber: string;
  orderNo: string;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  providerCode: string;
  status: string;
  transactionCount: number;
  usageAmounts: CardUsageAmount[];
}

export interface CardListParams {
  apiClientCode?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerAccountId?: number;
  providerCode?: string;
}

export function getCardsApi(params: CardListParams) {
  return adminClient.get<PagedResponse<CardListItem>>('/cards', { params });
}

export interface ProviderEvent {
  billingAmount: number;
  billingCurrency: string;
  eventStatus: string;
  eventType: string;
  failureReason?: string;
  id: number;
  processResult: string;
  providerCreatedAt: string;
  providerTransactionEventId?: number;
  rawJson: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  syncedAt: string;
  transactionAmount: number;
  transactionCurrency: string;
}

export interface NormalizedEvent {
  billingAmount: number;
  billingCurrency: string;
  createdAt: string;
  eventNo: string;
  eventStatus: string;
  eventType: string;
  failureReason?: string;
  id: number;
  occurredAt: string;
  processResult: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  transactionAmount: number;
  transactionCurrency: string;
}

export interface EventPair {
  normalizedEvent?: NormalizedEvent;
  providerEvent: ProviderEvent;
}

export interface ProviderLifecycle {
  billingAmounts: BillingAmount[];
  firstSyncedAt?: string;
  id: number;
  lastSyncedAt?: string;
  providerCardId: string;
  providerCreatedAt: string;
  providerLifecycleId: string;
  providerUpdatedAt: string;
  rawJson: string;
}

export interface BillingAmount {
  currency: string;
  id: number;
  lastSyncedAt?: string;
  totalAuthorized: number;
  totalCredited: number;
  totalDebited: number;
  totalDeclined: number;
  totalExpired: number;
  totalPending: number;
  totalReversed: number;
}

export interface ProviderTransaction {
  billingAmount: number;
  billingCurrency: string;
  direction: string;
  id: number;
  merchantCategoryCode: string;
  merchantName: string;
  providerCardId: string;
  providerCardLifecycleId: number;
  providerCardTransactionId: string;
  providerCreatedAt: string;
  providerUpdatedAt: string;
  rawJson: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  transactionAmount: number;
  transactionCurrency: string;
  transactionStatus: string;
  transactionType: string;
}

export interface NormalizedTransaction {
  amountCurrency: string;
  amountValue: number;
  billingAmount: number;
  billingCurrency: string;
  createdAt: string;
  id: number;
  isTerminal: boolean;
  lastObservedAt?: string;
  providerAccountId: number;
  providerCardId: string;
  providerTransactionId?: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  status: string;
  terminalAt?: string;
  transactionCurrency: string;
  transactionAmount: number;
  transactionNo: string;
  transactionTime: string;
  transactionType: string;
  updatedAt: string;
  virtualCardId: number;
}

export interface TransactionPair {
  events: EventPair[];
  normalizedTransaction?: NormalizedTransaction;
  providerLifecycle?: ProviderLifecycle;
  providerTransaction: ProviderTransaction;
}

export interface CardChange {
  apiClientCode: string;
  apiClientName: string;
  apiKeyName: string;
  beforeValueJson: string;
  afterValueJson: string;
  changeLogId: number;
  changeType: string;
  currency?: string;
  ipAddress: string;
  providerCompletedAt?: string;
  providerErrorCode?: string;
  providerErrorMessage?: string;
  providerSyncStatus: string;
  requestedAt: string;
  requestId: string;
}

export interface CardHeader {
  apiClientCode: string;
  apiClientName?: string;
  businessOrderNo: string;
  cardholderId: number;
  cardholderName?: string;
  createdAt: string;
  email?: string;
  expectedBusinessStatus: number;
  hotelOrder?: HotelOrder;
  id: number;
  individualAddressCity?: string;
  individualAddressCountry?: string;
  individualNationality?: string;
  limitAmountCurrency: string;
  limitAmountValue: number;
  maskedCardNumber: string;
  mobileNumber?: string;
  orderNo: string;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  providerCode: string;
  status: string;
  type?: string;
  cardholderStatus?: string;
}

export interface CardTransactionsResponse {
  card: CardHeader;
  changes: CardChange[];
  transactions: TransactionPair[];
  normalizedLifecycles: NormalizedLifecycle[];
  usageAmounts: CardUsageAmount[];
}

export interface NormalizedLifecycle {
  billingAmounts: NormalizedLifecycleBillingAmount[];
  id: number;
  lastObservedAt: string;
  lifecycleCreatedAt: string;
  lifecycleUpdatedAt: string;
  providerCardId: string;
  providerCode: string;
  providerLifecycleId: string;
  status: string;
  transactions: NormalizedTransactionNode[];
}

export interface NormalizedLifecycleBillingAmount {
  currency: string;
  id: number;
  totalAuthorized: number;
  totalCredited: number;
  totalDebited: number;
  totalDeclined: number;
  totalExpired: number;
  totalPending: number;
  totalReversed: number;
  updatedAt: string;
}

export interface NormalizedTransactionNode {
  amountCurrency: string;
  amountValue: number;
  billingAmount: number;
  billingCurrency: string;
  createdAt: string;
  events: NormalizedEventNode[];
  id: number;
  isTerminal: boolean;
  lastObservedAt?: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  terminalAt?: string;
  transactionAmount: number;
  transactionCurrency: string;
  transactionNo: string;
  transactionStatus: string;
  transactionTime: string;
  transactionType: number;
  updatedAt: string;
}

export interface NormalizedEventNode {
  billingAmount: number;
  billingCurrency: string;
  createdAt: string;
  eventNo: string;
  eventStatus: string;
  eventType: string;
  failureReason?: string;
  id: number;
  occurredAt: string;
  processResult: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  transactionAmount: number;
  transactionCurrency: string;
}

export function getCardTransactionsApi(cardId: number) {
  return adminClient.get<CardTransactionsResponse>(`/cards/${cardId}/transactions`);
}

export interface ProviderAccountOption {
  accountName: string;
  createdAt: string;
  id: number;
  isActive: boolean;
  lastSuccessfulSyncedAt?: string;
  providerCode: string;
}

export function getProviderAccountOptionsApi() {
  return adminClient.get<ProviderAccountOption[]>('/provider-accounts');
}

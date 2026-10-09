import type { PagedResponse } from './types';

import { adminClient } from './admin-client';

export interface AirwallexTransactionListItem {
  apiClientCode?: string;
  apiClientName?: string;
  billingAmount: number;
  billingCurrency: string;
  cardId?: number;
  direction: string;
  eventCount: number;
  id: number;
  lastSyncedAt: string;
  maskedCardNumber?: string;
  merchantCategoryCode: string;
  merchantName: string;
  normalizedEventCount: number;
  normalizedTransactionId?: number;
  orderNo?: string;
  projectionState: string;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  providerCreatedAt: string;
  providerTransactionId: string;
  status: string;
  transactionAmount: number;
  transactionCurrency: string;
  type: string;
}

export interface TranHubTransactionListItem {
  apiClientCode?: string;
  apiClientName?: string;
  bankProcessBillType?: string;
  billAmount: number;
  billCurrency?: string;
  billNo: string;
  cardId?: number;
  cardNo?: string;
  cardOrderNo?: string;
  direction: string;
  firstSyncedAt: string;
  id: number;
  lastSyncedAt: string;
  maskedCardNumber?: string;
  merTradeNo?: string;
  normalizedCardTransactionId?: number;
  providerAccountId: number;
  providerAccountName: string;
  remark?: string;
  saleAmount: number;
  saleCurrency?: string;
  settleDateTime?: string;
  status: string;
  tranDateTime?: string;
  tranType: string;
}

export interface ProviderTransactionListParams {
  apiClientCode?: string;
  from?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerAccountId?: number;
  status?: string;
  to?: string;
}

export function getAirwallexTransactionsApi(params: ProviderTransactionListParams) {
  return adminClient.get<PagedResponse<AirwallexTransactionListItem>>(
    '/airwallex-transactions',
    { params },
  );
}

export function getTranHubTransactionsApi(params: ProviderTransactionListParams) {
  return adminClient.get<PagedResponse<TranHubTransactionListItem>>('/tranhub-transactions', {
    params,
  });
}

export interface NormalizedTransactionListItem {
  apiClientCode?: string;
  apiClientName?: string;
  billingAmount: number;
  billingCurrency: string;
  cardId: number;
  eventCount: number;
  id: number;
  lastObservedAt?: string;
  maskedCardNumber: string;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  providerTransactionId?: string;
  status: string;
  isTerminal: boolean;
  transactionAmount: number;
  transactionCurrency: string;
  transactionNo: string;
  transactionTime: string;
}

export interface NormalizedTransactionListParams {
  apiClientCode?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerAccountId?: number;
  providerCode?: string;
}

export function getNormalizedTransactionsApi(params: NormalizedTransactionListParams) {
  return adminClient.get<PagedResponse<NormalizedTransactionListItem>>(
    '/normalized-transactions',
    { params },
  );
}

export interface UnprojectedTransactionListItem {
  apiClientCode?: string;
  apiClientName?: string;
  amount: number;
  currency: string;
  failureReason?: string;
  failureStage: string;
  id: number;
  issueType?: string;
  lastFailureAt?: string;
  lastSyncedAt: string;
  merchantName: string;
  normalizedTransactionId?: number;
  providerAccountId: number;
  providerAccountName: string;
  providerCardId: string;
  providerCode: string;
  providerCreatedAt: string;
  providerTransactionId: string;
  merTradeNo?: string;
  status: string;
  type: string;
  unprojectedEventCount: number;
}

export interface UnprojectedTransactionListParams {
  apiClientCode?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerCode?: string;
}

export function getUnprojectedTransactionsApi(params: UnprojectedTransactionListParams) {
  return adminClient.get<PagedResponse<UnprojectedTransactionListItem>>(
    '/unprojected-transactions',
    { params },
  );
}

export interface UnlinkedTransactionItem {
  billingAmount: number;
  billingCurrency: string;
  cardLifecycleId?: number;
  direction: string;
  id: number;
  linkState: string;
  merchantCategoryCode: string;
  merchantName: string;
  providerAccountId: number;
  providerCardId: string;
  providerCardLifecycleId: number;
  providerCardTransactionId: string;
  providerCode: string;
  providerCreatedAt: string;
  providerLifecycle?: object;
  providerLifecycleId: number;
  providerUpdatedAt: string;
  rawJson: string;
  realDeductAmount?: number;
  realDeductAmountSource?: string;
  realDeductCurrency?: string;
  merTradeNo?: string;
  status: string;
  transactionAmount: number;
  transactionCurrency: string;
  transactionType: string;
  virtualCardId?: number;
}

export interface UnlinkedTransactionListParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
  providerCode?: string;
}

export function getUnlinkedTransactionsApi(params: UnlinkedTransactionListParams) {
  return adminClient.get<PagedResponse<UnlinkedTransactionItem>>('/unlinked-transactions', {
    params,
  });
}

export interface TransactionSummary {
  normalizedFactCount: number;
  providerFactCount: number;
  tranHubExternalCount: number;
  tranHubFailedCount: number;
  tranHubPendingCount: number;
  unprojectedEventCount: number;
  unprojectedTransactionCount: number;
}

export function getTransactionSummaryApi() {
  return adminClient.get<TransactionSummary>('/transactions/summary');
}

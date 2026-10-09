import { adminClient } from './admin-client';

export interface RawTransactionQueryParams {
  billType?: string;
  endDate: string;
  maxRecords?: number;
  page?: number;
  pageSize?: number;
  providerAccountId: number;
  startDate: string;
}

export interface RawTransactionItem {
  billingAmount: number;
  billingCurrency: string;
  cardNumber?: string;
  merchantName?: string;
  providerTransactionId: string;
  rawJson?: string;
  remark?: string;
  settleTime?: string;
  status?: string;
  transactionAmount: number;
  transactionCurrency: string;
  transactionTime?: string;
  transactionType: string;
}

export interface RawTransactionQueryResult {
  endDate: string;
  hasMore: boolean;
  items: RawTransactionItem[];
  providerAccountId: number;
  providerAccountName: string;
  providerCode: string;
  startDate: string;
  totalCount: number;
}

export function queryProviderRawTransactionsApi(data: RawTransactionQueryParams) {
  return adminClient.post<RawTransactionQueryResult>('/provider-raw-transactions/query', data);
}

export interface TranHubBackfillParams {
  endDate: string;
  providerAccountId?: number;
  startDate: string;
  updateWatermark?: boolean;
}

export interface TranHubBackfillResult {
  endDate: string;
  externalEvents: number;
  failedEvents: number;
  isDisabled: boolean;
  normalizedEvents: number;
  providerAccountId?: number;
  startDate: string;
  syncRunId: string;
  syncedEvents: number;
  updateWatermark: boolean;
}

export function backfillTranHubTransactionsApi(data: TranHubBackfillParams) {
  return adminClient.post<TranHubBackfillResult>('/transactions/tranhub/backfill', data);
}

import type { EnumLabelEntry } from '#/components/enum-label.vue';

export const DIRECTION_LABELS: Record<string, EnumLabelEntry> = {
  CREDIT: { label: '贷记', description: '入账 / 退款，金额增加', type: 'success' },
  DEBIT: { label: '借记', description: '扣款 / 消费，金额减少', type: 'warning' },
};

export const AIRWALLEX_STATUS_LABELS: Record<string, EnumLabelEntry> = {
  AUTHORIZED: { label: '已授权', description: '交易已授权，尚未清算', type: 'primary' },
  CLEARED: { label: '已清算', description: '交易已完成清算', type: 'success' },
  CLOSED: { label: '已关闭', description: '授权已关闭 / 撤销', type: 'info' },
  DECLINED: { label: '已拒绝', description: '交易被拒绝', type: 'danger' },
  EXPIRED: { label: '已过期', description: '授权已过期未完成', type: 'info' },
  FAILED: { label: '失败', description: '交易失败', type: 'danger' },
  PENDING: { label: '处理中', description: '交易等待处理', type: 'warning' },
  SETTLED: { label: '已结算', description: '交易已结算入账', type: 'success' },
  SUCCEEDED: { label: '已成功', description: '交易成功完成', type: 'success' },
  VOIDED: { label: '已作废', description: '交易已作废', type: 'info' },
};

export const TRANHUB_STATUS_LABELS: Record<string, EnumLabelEntry> = {
  External: { label: '外部交易', description: '非本平台发卡产生的交易，不参与投影', type: 'info' },
  Failed: { label: '标准化失败', description: '交易标准化处理失败，需人工排查', type: 'danger' },
  Normalized: { label: '已标准化', description: '交易已成功标准化并关联', type: 'success' },
  Pending: { label: '待处理', description: '交易等待标准化处理', type: 'warning' },
};

export const TRANHUB_TRANTYPE_LABELS: Record<string, EnumLabelEntry> = {
  AUTH: { label: '授权', description: '交易授权', type: 'primary' },
  AUTHREVERSAL: { label: '授权撤销', description: '授权冲正 / 撤销（Reversal）', type: 'info' },
  REFUND: { label: '退款', description: '退款交易', type: 'success' },
  SETTLE: { label: '结算', description: '结算 / 清算交易', type: 'success' },
};

export const PROJECTION_STATE_LABELS: Record<string, EnumLabelEntry> = {
  Normalized: { label: '已投影', description: '全部事件均已投影到标准化交易', type: 'success' },
  Partial: { label: '部分投影', description: '部分事件尚未成功投影', type: 'warning' },
  Unprojected: { label: '未投影', description: '尚未投影到标准化交易', type: 'danger' },
};

// CardTransactionType 枚举（后端 int）：0=Payment 1=Refund 2=Reversal 3=Fee
export const NORMALIZED_TRANSACTION_TYPE_LABELS: Record<string, EnumLabelEntry> = {
  0: { label: '支付', description: 'Payment', type: 'primary' },
  1: { label: '退款', description: 'Refund', type: 'success' },
  2: { label: '冲正', description: 'Reversal', type: 'info' },
  3: { label: '手续费', description: 'Fee', type: 'warning' },
};

// 标准化交易状态（CardTransaction.TransactionStatus）
export const NORMALIZED_STATUS_LABELS: Record<string, EnumLabelEntry> = {
  AUTHORIZED: { label: '已授权', description: 'Authorized', type: 'primary' },
  FAILED: { label: '失败', description: 'Failed', type: 'danger' },
  PENDING: { label: '处理中', description: 'Pending', type: 'warning' },
  REFUNDED: { label: '已退款', description: 'Refunded', type: 'success' },
  REVERSED: { label: '已冲正', description: 'Reversed', type: 'info' },
  SETTLED: { label: '已结算', description: 'Settled', type: 'success' },
};

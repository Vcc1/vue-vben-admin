export function cardStatus(status: number | string) {
  switch (Number(status)) {
    case 0: return { label: '已创建', type: 'info' as const };
    case 1: return { label: '激活', type: 'success' as const };
    case 2: return { label: '已关闭', type: 'danger' as const };
    case 3: return { label: '已过期', type: 'warning' as const };
    case 4: return { label: '失败', type: 'danger' as const };
    default: return { label: String(status), type: 'info' as const };
  }
}

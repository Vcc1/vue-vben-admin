import type { SessionApi } from '#/api/session';

export function formatTime(dt?: null | string) {
  return dt ? new Date(dt).toLocaleString() : '-';
}

export function deviceText(device?: null | SessionApi.DeviceInfo) {
  const parts = [device?.clientName, device?.os, device?.model].filter(Boolean);
  return parts.length > 0 ? parts.join(' / ') : '未知设备';
}

const LOGIN_METHODS: Record<string, string> = {
  otp: '验证码',
  pwd: '密码',
  sso: '单点登录',
};

export function loginMethodText(method: string) {
  return LOGIN_METHODS[method] ?? method;
}

export type TagType = 'danger' | 'info' | 'primary' | 'success' | 'warning';

export function levelTagType(level: number): TagType {
  return level >= 3 ? 'danger' : level === 2 ? 'warning' : 'info';
}

const DEVICE_STATUS: Record<string, { label: string; type: TagType }> = {
  never_trusted: { label: '从未受信', type: 'info' },
  revoked: { label: '已踢除', type: 'danger' },
  trusted: { label: '受信', type: 'success' },
};

export function deviceStatusLabel(status: string) {
  return DEVICE_STATUS[status]?.label ?? status;
}

export function deviceStatusTagType(status: string): TagType {
  return DEVICE_STATUS[status]?.type ?? 'info';
}

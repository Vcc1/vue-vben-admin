import type { UserInfo } from '@vben/types';

import { requestClient } from '#/api/request';

export type OidcUserInfo = UserInfo & {
  email?: string;
  permissions?: string[];
};

export async function getUserInfoApi(): Promise<OidcUserInfo> {
  const resp = await requestClient.get<Record<string, any>>('/auth/me');

  const toArray = (value: unknown): string[] =>
    Array.isArray(value) ? value : value ? [String(value)] : [];

  return {
    avatar: '',
    desc: '',
    email: resp.email || undefined,
    homePath: '',
    permissions: toArray(resp.permission),
    realName: resp.display_name || resp.name || resp.preferred_username || '',
    roles: toArray(resp.role),
    token: '',
    userId: resp.sub || '',
    username: resp.preferred_username || resp.name || '',
  };
}

import type { UserInfo } from '@vben/types';

import { baseRequestClient } from '#/api/request';

/**
 * GET /api/admin/auth/me
 */
export async function getUserInfoApi(): Promise<UserInfo> {
  const resp = await baseRequestClient.get<{
    displayName: string;
    email: string;
    id: string;
    name: string;
    phoneNumber: string;
    roles: string[];
    userName: string;
  }>('/admin/auth/me');

  return {
    avatar: '',
    desc: '',
    email: resp.email ?? '',
    homePath: '',
    name: resp.name ?? '',
    phoneNumber: resp.phoneNumber ?? '',
    realName: resp.displayName || resp.userName,
    roles: resp.roles,
    token: '',
    userId: resp.id,
    username: resp.userName,
  };
}

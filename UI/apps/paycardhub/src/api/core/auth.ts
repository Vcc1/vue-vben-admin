import { requestClient } from '#/api/request';
import { useAccessStore } from '@vben/stores';

const apiURL = import.meta.env.VITE_GLOB_API_URL as string;

export function startOidcLogin(): void {
  window.location.href = `${apiURL}/auth/login`;
}

export async function getAccessCodesApi(): Promise<string[]> {
  const resp = await requestClient.get<Record<string, any>>('/auth/me');

  const codes = resp?.permission;
  return Array.isArray(codes) ? codes : codes ? [codes] : [];
}

export async function handleUnauthorized(): Promise<void> {
  const accessStore = useAccessStore();
  accessStore.setAccessToken(null);
  accessStore.setRefreshToken(null);
  window.location.href = `${apiURL}/auth/login`;
}

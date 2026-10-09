import type { SessionApi } from '#/api/session';

import { baseRequestClient } from '#/api/request';

export namespace AuthApi {
  export interface LoginParams {
    captchaAnswer?: string;
    captchaId?: string;
    password: string;
    rememberMe?: boolean;
    username: string;
  }

  export interface LoginResponse {
    stepUp?: null | SessionApi.StepUpChallenge;
    tokens: null | SessionApi.Tokens;
    user: {
      displayName: string;
      id: string;
      userName: string;
    };
  }

  export interface RefreshResponse {
    accessToken: string;
    refreshToken: string;
  }

  export interface CaptchaRequiredResponse {
    required: boolean;
  }

  export interface ImageCaptchaResponse {
    captchaId: string;
    imageBase64: string;
  }
}

function detectOs(userAgent: string): string {
  if (/windows nt/i.test(userAgent)) return 'Windows';
  if (/android/i.test(userAgent)) return 'Android';
  if (/iphone|ipad|ipod/i.test(userAgent)) return 'iOS';
  if (/mac os x/i.test(userAgent)) return 'macOS';
  if (/linux/i.test(userAgent)) return 'Linux';
  return 'Unknown';
}

function detectBrowser(userAgent: string): string {
  if (/edg\//i.test(userAgent)) return 'Edge';
  if (/chrome\//i.test(userAgent)) return 'Chrome';
  if (/firefox\//i.test(userAgent)) return 'Firefox';
  if (/safari\//i.test(userAgent)) return 'Safari';
  return 'Unknown';
}

/**
 * 浏览器上报的设备展示信息，只用于"我的设备"列表可读性。
 * deviceId 不由前端生成——服务端用 AS 域下的 cookie 承载，前端读不到也不需要读。
 */
export function collectDeviceInfo() {
  const userAgent = navigator.userAgent;
  return {
    clientKind: 'web',
    clientName: `MengtuV Admin (${detectBrowser(userAgent)})`,
    language: navigator.language,
    os: detectOs(userAgent),
    platform: navigator.platform || 'unknown',
    screen: `${window.screen.width}x${window.screen.height}`,
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  };
}

/**
 * POST /api/admin/auth/login (mode=token)
 * Sets auth cookie + returns JWT tokens.
 * 设备不受信时返回 HTTP 428，body 里带 stepUp 挑战。
 */
export async function loginApi(data: AuthApi.LoginParams) {
  const resp = await baseRequestClient.post<AuthApi.LoginResponse>(
    '/admin/auth/login',
    {
      captchaAnswer: data.captchaAnswer,
      captchaId: data.captchaId,
      deviceInfo: collectDeviceInfo(),
      mode: 'token',
      password: data.password,
      rememberMe: data.rememberMe ?? false,
      userName: data.username,
    },
  );

  return {
    accessToken: resp.tokens?.accessToken ?? '',
    refreshToken: resp.tokens?.refreshToken ?? '',
  };
}

export async function checkCaptchaRequiredApi(
  userName?: string,
): Promise<AuthApi.CaptchaRequiredResponse> {
  const resp = await baseRequestClient.get<AuthApi.CaptchaRequiredResponse>(
    '/admin/auth/captcha-required',
    { params: userName ? { userName } : {} },
  );
  return resp;
}

export async function getImageCaptchaApi(): Promise<AuthApi.ImageCaptchaResponse> {
  return baseRequestClient.get<AuthApi.ImageCaptchaResponse>('/admin/auth/captcha/image');
}

/**
 * POST /api/admin/auth/refresh
 */
export async function refreshTokenApi(refreshToken: string) {
  const resp = await baseRequestClient.post<AuthApi.RefreshResponse>(
    '/admin/auth/refresh',
    { refreshToken },
  );

  return {
    accessToken: resp.accessToken,
    refreshToken: resp.refreshToken,
  };
}

/**
 * POST /api/admin/auth/logout
 */
export async function logoutApi() {
  await baseRequestClient.post('/admin/auth/logout');
}

/**
 * GET /api/admin/auth/permissions
 * 获取当前用户的权限码列表
 */
export async function getAccessCodesApi(): Promise<string[]> {
  return baseRequestClient.get<string[]>('/admin/auth/permissions');
}

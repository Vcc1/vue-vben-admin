import { baseRequestClient } from '#/api/request';

export namespace SessionApi {
  /** 后端 DeviceInfoSummary：只解析已知字段，未知字段保留在原始 JSON 中。 */
  export interface DeviceInfo {
    clientKind?: null | string;
    clientName?: null | string;
    model?: null | string;
    os?: null | string;
    platform?: null | string;
    timezone?: null | string;
  }

  export interface SessionItem {
    clients: string[];
    createdAtUtc: string;
    device?: DeviceInfo | null;
    deviceId?: null | string;
    displayName: string;
    expiresAtUtc: string;
    ipAddress?: null | string;
    isCurrent: boolean;
    isOnline: boolean;
    isRevoked: boolean;
    lastActivityAtUtc: string;
    level: number;
    loginMethod: string;
    revokedAtUtc?: null | string;
    revokedReason?: null | string;
    sid: string;
    userAgent?: null | string;
    userId: string;
    userName: string;
  }

  export interface DeviceItem {
    activeSessionCount: number;
    device?: DeviceInfo | null;
    deviceId: string;
    firstSeenUtc: string;
    isCurrentDevice: boolean;
    isOnline: boolean;
    isTrusted: boolean;
    lastSeenUtc: string;
    revokedAtUtc?: null | string;
    revokedReason?: null | string;
    sessions: SessionItem[];
    /** trusted / revoked / never_trusted */
    status: string;
  }

  /** 按设备反查：这台设备被哪些用户登录过（跨用户，含已踢除记录）。 */
  export interface DeviceUsageItem {
    deviceId: string;
    displayName: string;
    firstSeenUtc: string;
    isOnline: boolean;
    lastSeenUtc: string;
    lastSessionAtUtc?: null | string;
    revokedAtUtc?: null | string;
    revokedReason?: null | string;
    sessionCount: number;
    status: string;
    userId: string;
    userName: string;
  }

  export interface CurrentSession {
    acr: string;
    clients: string[];
    createdAtUtc: string;
    device?: DeviceInfo | null;
    deviceId?: null | string;
    expiresAtUtc: string;
    lastActivityAtUtc: string;
    level: number;
    loginMethod: string;
    passwordVerifiedAtUtc?: null | string;
    sid: string;
    strongAuthAtUtc?: null | string;
    strongAuthMethod?: null | string;
  }

  /** 登录返回 428 时携带的挑战；channels 为可用验证方式，maskedTarget 已脱敏。 */
  export interface StepUpChallenge {
    channels: string[];
    expiresInSeconds: number;
    maskedTarget?: null | string;
    reason: string;
    stepUpToken: string;
  }

  export interface Tokens {
    accessToken: string;
    accessTokenExpiresAtUtc: string;
    refreshToken: string;
    refreshTokenExpiresAtUtc: string;
  }

  export interface LoginResult {
    stepUp?: null | StepUpChallenge;
    tokens?: null | Tokens;
    user: {
      displayName: string;
      id: string;
      userName: string;
    };
  }

  /** 401 stepup_required 响应体的扩展字段。 */
  export interface StepUpRequired {
    channels: string[];
    currentLevel: number;
    requiredLevel: number;
  }
}

export async function getSessionApi() {
  return baseRequestClient.get<null | SessionApi.CurrentSession>(
    '/admin/auth/session',
  );
}

/** 登录二次验证：发送验证码。 */
export async function sendLoginStepUpCodeApi(stepUpToken: string, channel: string) {
  return baseRequestClient.post('/admin/auth/stepup/send', {
    channel,
    stepUpToken,
  });
}

/** 登录二次验证：校验验证码并完成登录。 */
export async function verifyLoginStepUpApi(data: {
  channel: string;
  credential: string;
  mode?: string;
  rememberMe?: boolean;
  stepUpToken: string;
}) {
  return baseRequestClient.post<SessionApi.LoginResult>(
    '/admin/auth/stepup/verify',
    { mode: 'token', rememberMe: false, ...data },
  );
}

/** 会话内提权：发送强认证验证码（提升到 L3）。 */
export async function sendSessionStepUpCodeApi(channel: string) {
  return baseRequestClient.post<SessionApi.StepUpChallenge>(
    '/admin/auth/session/stepup/send',
    null,
    { params: { channel } },
  );
}

/**
 * 会话内提权：password 通道核验密码提升到 L2，其余通道提升到 L3。
 * 成功后会重签 cookie 并重发 token，前端需要用返回的 tokens 覆盖本地凭据。
 */
export async function verifySessionStepUpApi(data: {
  channel: string;
  credential: string;
}) {
  return baseRequestClient.post<SessionApi.LoginResult>(
    '/admin/auth/session/stepup/verify',
    data,
  );
}

export async function listMySessionsApi(includeRevoked = false) {
  return baseRequestClient.get<SessionApi.SessionItem[]>('/admin/my/sessions', {
    params: { includeRevoked },
  });
}

export async function revokeMySessionApi(sid: string, reason?: string) {
  return baseRequestClient.post(`/admin/my/sessions/${sid}/revoke`, { reason });
}

export async function listMyDevicesApi() {
  return baseRequestClient.get<SessionApi.DeviceItem[]>('/admin/my/devices');
}

export async function revokeMyDeviceApi(deviceId: string, reason?: string) {
  return baseRequestClient.post(
    `/admin/my/devices/${encodeURIComponent(deviceId)}/revoke`,
    { reason },
  );
}

export async function listSessionsApi(params: {
  includeRevoked?: boolean;
  userId?: string;
} = {}) {
  return baseRequestClient.get<SessionApi.SessionItem[]>('/admin/sessions', {
    params,
  });
}

export async function revokeSessionApi(sid: string, reason?: string) {
  return baseRequestClient.post(`/admin/sessions/${sid}/revoke`, { reason });
}

export async function revokeUserSessionsApi(userId: string, reason?: string) {
  return baseRequestClient.post<{ revoked: number }>(
    `/admin/users/${userId}/sessions/revoke`,
    { reason },
  );
}

export async function listUserDevicesApi(userId: string) {
  return baseRequestClient.get<SessionApi.DeviceItem[]>('/admin/devices', {
    params: { userId },
  });
}

/** 反查某台设备被哪些用户登录过，含已踢出受信表的记录。 */
export async function listDeviceUsageApi(deviceId: string) {
  return baseRequestClient.get<SessionApi.DeviceUsageItem[]>(
    `/admin/devices/${encodeURIComponent(deviceId)}/usage`,
  );
}

export async function revokeDeviceApi(
  userId: string,
  deviceId: string,
  reason?: string,
) {
  return baseRequestClient.post(
    `/admin/users/${userId}/devices/${encodeURIComponent(deviceId)}/revoke`,
    { reason },
  );
}

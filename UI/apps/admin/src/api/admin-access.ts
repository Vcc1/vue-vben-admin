import { baseRequestClient } from '#/api/request';

export interface AdminUser {
  id: string;
  userName: string;
  name: string;
  displayName: string;
  email: null | string;
  isEnabled: boolean;
  roles: string[];
  createdAtUtc: string;
  lastLoginAtUtc: null | string;
}

export interface AdminRole {
  name: string;
  isSystem: boolean;
  userCount: number;
  permissions: string[];
}

export interface PermissionNode {
  code: string;
  name: string;
  description: null | string;
  isEnabled: boolean | null;
  children: PermissionNode[];
}

export interface CreateUserRequest {
  userName: string;
  name: string;
  displayName: string;
  email: null | string;
  password: string;
  roles: string[];
}

export interface UpdateUserRequest {
  name: string;
  displayName: string;
  email: null | string;
}

export interface PermissionDetails {
  name: string;
  description: null | string;
  isEnabled: boolean;
}

export type TransferKind = 'permissions' | 'roles';
export interface TransferPermission extends PermissionDetails { code: string }
export interface TransferRole { name: string; permissions: string[] }
export interface AccessTransferDocument {
  format: string;
  version: number;
  kind: TransferKind;
  permissions: null | TransferPermission[];
  roles: null | TransferRole[];
}
export interface AccessTransferChange {
  key: string;
  action: 'add' | 'delete' | 'update';
  beforePermission: null | TransferPermission;
  afterPermission: null | TransferPermission;
  beforeRole: null | TransferRole;
  afterRole: null | TransferRole;
  blockReason: null | string;
  impact: null | string;
}
export interface AccessTransferPreview {
  baseline: string;
  changes: AccessTransferChange[];
}

export const listUsersApi = () => baseRequestClient.get<AdminUser[]>('/admin/users');
export const createUserApi = (data: CreateUserRequest) =>
  baseRequestClient.post<AdminUser>('/admin/users', data);
export const setUserEnabledApi = (id: string, isEnabled: boolean) =>
  baseRequestClient.put(`/admin/users/${id}/enabled`, { isEnabled });
export const resetUserPasswordApi = (id: string, newPassword: string) =>
  baseRequestClient.put(`/admin/users/${id}/password`, { newPassword });
export const setUserRolesApi = (id: string, roles: string[]) =>
  baseRequestClient.put(`/admin/users/${id}/roles`, { roles });
export const updateUserApi = (id: string, data: UpdateUserRequest) =>
  baseRequestClient.put<AdminUser>(`/admin/users/${id}/profile`, data);

export const listRolesApi = () => baseRequestClient.get<AdminRole[]>('/admin/roles');
export const createRoleApi = (name: string) =>
  baseRequestClient.post<AdminRole>('/admin/roles', { name });
export const setRolePermissionsApi = (name: string, permissions: string[]) =>
  baseRequestClient.put(`/admin/roles/${encodeURIComponent(name)}/permissions`, { permissions });
export const deleteRoleApi = (name: string) =>
  baseRequestClient.delete(`/admin/roles/${encodeURIComponent(name)}`);

export const listPermissionsApi = () =>
  baseRequestClient.get<PermissionNode[]>('/admin/permissions');
export const createPermissionApi = (data: PermissionDetails & { code: string }) =>
  baseRequestClient.post<PermissionNode>('/admin/permissions', data);
export const updatePermissionApi = (code: string, data: PermissionDetails) =>
  baseRequestClient.put<PermissionNode>(
    `/admin/permissions/${encodeURIComponent(code)}`,
    data,
  );
export const deletePermissionApi = (code: string) =>
  baseRequestClient.delete(`/admin/permissions/${encodeURIComponent(code)}`);

export const exportAccessApi = (kind: TransferKind) =>
  baseRequestClient.get<AccessTransferDocument>(`/admin/access-transfer/${kind}`);
export const compareAccessApi = (document: AccessTransferDocument) =>
  baseRequestClient.post<AccessTransferPreview>('/admin/access-transfer/compare', document);
export const applyAccessApi = (
  document: AccessTransferDocument, baseline: string, selectedKeys: string[],
) => baseRequestClient.post<{ appliedCount: number }>(
  '/admin/access-transfer/apply', { document, baseline, selectedKeys },
);

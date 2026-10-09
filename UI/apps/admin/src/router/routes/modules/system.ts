import type { RouteRecordRaw } from 'vue-router';

import { $t } from '#/locales';

// 三级菜单：系统设置作为顶层分组，下挂 用户与权限 / OIDC / 定时任务 / 审计日志。
// 各分组沿用原有绝对路径，URL 保持不变（菜单链接由 router.getRoutes() 按 name 解析）。
const routes: RouteRecordRaw[] = [
  {
    meta: {
      authority: [
        'auth:admin:users:read',
        'auth:admin:roles:read',
        'auth:admin:permissions:read',
        'auth:admin:oidc:clients:read',
        'auth:admin:oidc:scopes:read',
        'auth:admin:sessions:read',
        'auth:admin:scheduling:read',
        'auth:admin:audit:read',
        'auth:admin:notification:read',
      ],
      icon: 'lucide:settings',
      order: 10,
      title: $t('page.system.title'),
    },
    name: 'System',
    path: '/system',
    children: [
      {
        name: 'AccessManagement',
        path: '/access',
        meta: {
          authority: ['auth:admin:users:read', 'auth:admin:roles:read', 'auth:admin:permissions:read'],
          icon: 'lucide:users-round',
          order: 1,
          title: $t('page.access.title'),
        },
        children: [
          {
            name: 'AdminUsers',
            path: 'users',
            component: () => import('#/views/access/users.vue'),
            meta: {
              authority: ['auth:admin:users:read'],
              icon: 'lucide:users',
              title: $t('page.access.users'),
            },
          },
          {
            name: 'AdminRoles',
            path: 'roles',
            component: () => import('#/views/access/roles.vue'),
            meta: {
              authority: ['auth:admin:roles:read'],
              icon: 'lucide:shield',
              title: $t('page.access.roles'),
            },
          },
          {
            name: 'AdminPermissions',
            path: 'permissions',
            component: () => import('#/views/access/permissions.vue'),
            meta: {
              authority: ['auth:admin:permissions:read'],
              icon: 'lucide:key-round',
              title: $t('page.access.permissions'),
            },
          },
        ],
      },
      {
        name: 'Oidc',
        path: '/oidc',
        meta: {
          authority: ['auth:admin:oidc:clients:read', 'auth:admin:oidc:scopes:read'],
          icon: 'lucide:shield-check',
          order: 2,
          title: $t('page.oidc.title'),
        },
        children: [
          {
            name: 'OidcClients',
            path: 'clients',
            component: () => import('#/views/oidc/clients.vue'),
            meta: {
              authority: ['auth:admin:oidc:clients:read'],
              icon: 'lucide:app-window',
              title: $t('page.oidc.clients'),
            },
          },
          {
            name: 'OidcScopes',
            path: 'scopes',
            component: () => import('#/views/oidc/scopes.vue'),
            meta: {
              authority: ['auth:admin:oidc:scopes:read'],
              icon: 'lucide:scan',
              title: $t('page.oidc.scopes'),
            },
          },
        ],
      },
      {
        name: 'SessionManagement',
        path: '/sessions',
        meta: {
          authority: ['auth:admin:sessions:read'],
          icon: 'lucide:monitor-smartphone',
          order: 3,
          title: $t('page.session.title'),
        },
        children: [
          {
            name: 'AdminSessions',
            path: 'all',
            component: () => import('#/views/sessions/index.vue'),
            meta: {
              authority: ['auth:admin:sessions:read'],
              icon: 'lucide:log-in',
              title: $t('page.session.all'),
            },
          },
        ],
      },
      {
        name: 'Scheduling',
        path: '/scheduling',
        meta: {
          authority: ['auth:admin:scheduling:read'],
          icon: 'lucide:clock',
          order: 4,
          title: $t('page.scheduling.title'),
        },
        children: [
          {
            name: 'ScheduledTasks',
            path: 'tasks',
            component: () => import('#/views/scheduled-tasks/index.vue'),
            meta: {
              authority: ['auth:admin:scheduling:read'],
              icon: 'lucide:list-checks',
              title: $t('page.scheduling.tasks'),
            },
          },
        ],
      },
      {
        name: 'Auditing',
        path: '/auditing',
        meta: {
          authority: ['auth:admin:audit:read'],
          icon: 'lucide:scroll-text',
          order: 5,
          title: $t('page.auditing.title'),
        },
        children: [
          {
            name: 'AuditSecurity',
            path: 'security',
            component: () => import('#/views/auditing/security.vue'),
            meta: {
              authority: ['auth:admin:audit:read'],
              icon: 'lucide:shield-alert',
              title: $t('page.auditing.security'),
            },
          },
          {
            name: 'AuditOperations',
            path: 'operations',
            component: () => import('#/views/auditing/operations.vue'),
            meta: {
              authority: ['auth:admin:audit:read'],
              icon: 'lucide:activity',
              title: $t('page.auditing.operations'),
            },
          },
        ],
      },
      {
        name: 'Notification',
        path: '/notification',
        meta: {
          authority: ['auth:admin:notification:read'],
          icon: 'lucide:bell',
          order: 6,
          title: $t('page.notification.title'),
        },
        children: [
          {
            name: 'NotificationCenter',
            path: 'center',
            component: () => import('#/views/notification/index.vue'),
            meta: {
              authority: ['auth:admin:notification:read'],
              icon: 'lucide:inbox',
              title: $t('page.notification.center'),
            },
          },
          {
            name: 'NotificationTypes',
            path: 'types',
            component: () => import('#/views/notification/types.vue'),
            meta: {
              authority: ['auth:admin:notification:read'],
              icon: 'lucide:tags',
              title: $t('page.notification.types'),
            },
          },
          {
            name: 'NotificationPublish',
            path: 'publish',
            component: () => import('#/views/notification/publish.vue'),
            meta: {
              authority: ['auth:admin:notification:publish'],
              icon: 'lucide:send',
              title: $t('page.notification.publish'),
            },
          },
        ],
      },
    ],
  },
];

export default routes;

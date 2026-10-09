import type { RouteRecordRaw } from 'vue-router';

import { preferences } from '@vben/preferences';

const BasicLayout = () => import('#/layouts/basic.vue');

const fallbackNotFoundRoute: RouteRecordRaw = {
  component: () => import('#/views/_core/fallback/not-found.vue'),
  meta: {
    hideInBreadcrumb: true,
    hideInMenu: true,
    hideInTab: true,
    title: '404',
  },
  name: 'FallbackNotFound',
  path: '/:path(.*)*',
};

const coreRoutes: RouteRecordRaw[] = [
  {
    component: BasicLayout,
    meta: {
      hideInBreadcrumb: true,
      title: 'Root',
    },
    name: 'Root',
    path: '/',
    redirect: preferences.app.defaultHomePath,
    children: [],
  },
  {
    path: '/auth',
    name: 'Authentication',
    children: [
      {
        name: 'OidcCallback',
        path: 'callback',
        component: () => import('#/views/_core/authentication/oidc-callback.vue'),
        meta: {
          hideInTab: true,
          title: 'Logging in...',
        },
      },
    ],
  },
];

export { coreRoutes, fallbackNotFoundRoute };

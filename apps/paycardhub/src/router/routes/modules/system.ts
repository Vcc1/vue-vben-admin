import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:settings',
      order: 3,
      title: '系统设置',
    },
    name: 'System',
    path: '/system',
    children: [
      {
        name: 'CardholderList',
        path: 'cardholders',
        component: () => import('#/views/cardholders/index.vue'),
        meta: {
          icon: 'lucide:users',
          title: '持卡人管理',
        },
      },
      {
        name: 'CardholderDetail',
        path: 'cardholders/:id',
        component: () => import('#/views/cardholders/detail.vue'),
        meta: {
          activePath: '/system/cardholders',
          hideInMenu: true,
          title: '持卡人详情',
        },
      },
      {
        name: 'ProviderAccountList',
        path: 'provider-accounts',
        component: () => import('#/views/provider-accounts/index.vue'),
        meta: {
          icon: 'lucide:building-2',
          title: '供应商账户',
        },
      },
      {
        name: 'ProviderAccountDetail',
        path: 'provider-accounts/:id',
        component: () => import('#/views/provider-accounts/detail.vue'),
        meta: {
          activePath: '/system/provider-accounts',
          hideInMenu: true,
          title: '账户详情',
        },
      },
      {
        name: 'ApiClientList',
        path: 'api-clients',
        component: () => import('#/views/api-clients/index.vue'),
        meta: {
          icon: 'lucide:key-round',
          title: 'API 客户端',
        },
      },
      {
        name: 'ApiClientDetail',
        path: 'api-clients/:id',
        component: () => import('#/views/api-clients/detail.vue'),
        meta: {
          activePath: '/system/api-clients',
          hideInMenu: true,
          title: '客户端详情',
        },
      },
    ],
  },
];

export default routes;

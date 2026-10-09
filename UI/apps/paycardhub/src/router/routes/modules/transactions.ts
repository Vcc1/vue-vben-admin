import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:arrow-right-left',
      order: 2,
      title: '交易管理',
    },
    name: 'Transactions',
    path: '/transactions',
    children: [
      {
        name: 'NormalizedTransactions',
        path: 'normalized',
        component: () => import('#/views/transactions/normalized.vue'),
        meta: {
          icon: 'lucide:check-circle',
          title: '标准化交易',
        },
      },
      {
        name: 'AirwallexGroup',
        path: 'airwallex',
        meta: {
          icon: 'lucide:database',
          title: 'Airwallex',
        },
        children: [
          {
            name: 'AirwallexTransactions',
            path: 'provider',
            component: () => import('#/views/transactions/airwallex.vue'),
            meta: {
              icon: 'lucide:credit-card',
              title: '供应商交易',
            },
          },
          {
            name: 'AirwallexUnprojected',
            path: 'unprojected',
            component: () => import('#/views/transactions/unprojected.vue'),
            meta: {
              icon: 'lucide:alert-triangle',
              title: '未投影交易',
              providerCode: 'airwallex',
            },
          },
          {
            name: 'AirwallexUnlinked',
            path: 'unlinked',
            component: () => import('#/views/transactions/unlinked.vue'),
            meta: {
              icon: 'lucide:unlink',
              title: '未关联交易',
              providerCode: 'airwallex',
            },
          },
        ],
      },
      {
        name: 'TranHubGroup',
        path: 'tranhub',
        meta: {
          icon: 'lucide:database',
          title: 'TranHub',
        },
        children: [
          {
            name: 'TranHubTransactions',
            path: 'provider',
            component: () => import('#/views/transactions/tranhub.vue'),
            meta: {
              icon: 'lucide:credit-card',
              title: '供应商交易',
            },
          },
          {
            name: 'TranHubUnprojected',
            path: 'unprojected',
            component: () => import('#/views/transactions/unprojected.vue'),
            meta: {
              icon: 'lucide:alert-triangle',
              title: '未投影交易',
              providerCode: 'tranhub',
            },
          },
          {
            name: 'TranHubUnlinked',
            path: 'unlinked',
            component: () => import('#/views/transactions/unlinked.vue'),
            meta: {
              icon: 'lucide:unlink',
              title: '未关联交易',
              providerCode: 'tranhub',
            },
          },
        ],
      },
      {
        name: 'RawTransactionQuery',
        path: 'raw-query',
        component: () => import('#/views/transactions/raw-query.vue'),
        meta: {
          icon: 'lucide:search',
          title: '原始交易查询',
        },
      },
    ],
  },
];

export default routes;

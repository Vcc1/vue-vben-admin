import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:credit-card',
      order: 1,
      title: '卡管理',
    },
    name: 'Cards',
    path: '/cards',
    children: [
      {
        name: 'CardList',
        path: '',
        component: () => import('#/views/cards/index.vue'),
        meta: {
          icon: 'lucide:list',
          title: '卡片列表',
        },
      },
      {
        name: 'CardDetail',
        path: ':id',
        component: () => import('#/views/cards/detail.vue'),
        meta: {
          hideInMenu: true,
          title: '卡片详情',
        },
      },
    ],
  },
];

export default routes;

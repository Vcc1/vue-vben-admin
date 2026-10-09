import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:activity',
      keepAlive: true,
      order: 10,
      title: '可观测性',
      authority: ['auth:admin:observability:read'],
    },
    name: 'observability',
    path: '/observability',
    children: [
      {
        meta: {
          icon: 'lucide:line-chart',
          keepAlive: false,
          title: '指标监控',
          authority: ['auth:admin:observability:read'],
        },
        name: 'observabilityMetrics',
        path: '/observability/metrics',
        component: () => import('#/views/observability/index.vue'),
      },
    ],
  },
];

export default routes;

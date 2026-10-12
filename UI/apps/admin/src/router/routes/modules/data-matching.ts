import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      authority: ['auth:admin:matching:read'],
      icon: 'lucide:git-compare',
      order: 5,
      title: '数据匹配',
    },
    name: 'DataMatching',
    path: '/data-matching',
    children: [
      {
        name: 'HotelMatching',
        path: 'hotel',
        component: () => import('#/views/data-matching/hotel.vue'),
        meta: {
          authority: ['auth:admin:matching:hotel:read'],
          icon: 'lucide:building-2',
          title: '酒店匹配',
        },
      },
      {
        name: 'RoomMatching',
        path: 'room',
        meta: {
          authority: ['auth:admin:matching:room:read'],
          icon: 'lucide:bed-double',
          title: '房型匹配',
        },
        redirect: '/data-matching/room/match',
        children: [
          {
            name: 'RoomMatchingList',
            path: 'match',
            component: () => import('#/views/data-matching/room/match.vue'),
            meta: {
              authority: ['auth:admin:matching:room:read'],
              icon: 'lucide:list',
              title: '匹配房型',
            },
          },
          {
            name: 'RoomMatchingCache',
            path: 'cache',
            component: () => import('#/views/data-matching/room/cache.vue'),
            meta: {
              authority: ['auth:admin:matching:room:read'],
              icon: 'lucide:database',
              title: '缓存查看',
            },
          },
          {
            name: 'RoomWordDictionary',
            path: 'dictionary',
            component: () => import('#/views/data-matching/room/dictionary.vue'),
            meta: {
              authority: ['auth:admin:matching:room:read'],
              icon: 'lucide:book-open',
              title: '房型词库',
            },
          },
        ],
      },
      {
        name: 'MatchingSettings',
        path: 'settings',
        component: () => import('#/views/data-matching/settings.vue'),
        meta: {
          authority: ['auth:admin:matching:settings:read'],
          icon: 'lucide:settings-2',
          title: '匹配设置',
        },
      },
    ],
  },
];

export default routes;

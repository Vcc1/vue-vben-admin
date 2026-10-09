import { defineConfig, viteCssLayerPlugin } from '@vben/vite-config';

import ElementPlus from 'unplugin-element-plus/vite';
import Components from 'unplugin-vue-components/vite';
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers';

// dev server 代理目标：默认本地后端；dev-admin-prod.ps1 会把它指到线上，避免浏览器直连跨域
const proxyTarget = process.env.VITE_PROXY_TARGET || 'http://localhost:5100';

export default defineConfig(async () => {
  return {
    application: {},
    vite: {
      plugins: [
        // element-plus 的 css 包进 @layer el，使 Tailwind 工具类可覆盖组件样式
        viteCssLayerPlugin({ layerName: 'el', packageName: 'element-plus' }),
        ElementPlus({ format: 'esm' }),
        // 自动注册模板里直接书写的 Element Plus 组件（如 <el-tabs>），供 observability 等迁移页面使用
        Components({
          resolvers: [ElementPlusResolver()],
          dts: 'src/components.d.ts',
        }),
      ],
      server: {
        proxy: {
          '/connect': {
            changeOrigin: true,
            target: proxyTarget,
            ws: true,
          },
          '/.well-known': {
            changeOrigin: true,
            target: proxyTarget,
          },
          '/api/connect': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            target: proxyTarget,
            ws: true,
          },
          '/api/.well-known': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            target: proxyTarget,
          },
          '/api': {
            changeOrigin: true,
            target: proxyTarget,
            ws: true,
          },
          '/hubs': {
            changeOrigin: true,
            target: proxyTarget,
            ws: true,
          },
        },
      },
    },
  };
});

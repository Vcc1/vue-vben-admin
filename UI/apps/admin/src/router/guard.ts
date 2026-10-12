import type { Router } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';
import { startProgress, stopProgress } from '@vben/utils';

import { ElNotification } from 'element-plus';

import { getAccessCodesApi } from '#/api';
import { accessRoutes, coreRouteNames } from '#/router/routes';
import { useAuthStore } from '#/store';

import { generateAccess } from './access';

/**
 * 通用守卫配置
 * @param router
 */
function setupCommonGuard(router: Router) {
  // 记录已经加载的页面
  const loadedPaths = new Set<string>();

  router.beforeEach((to) => {
    to.meta.loaded = loadedPaths.has(to.path);

    // 页面加载进度条
    if (!to.meta.loaded && preferences.transition.progress) {
      startProgress();
    }
    return true;
  });

  router.afterEach((to) => {
    // 记录页面是否加载,如果已经加载，后续的页面切换动画等效果不在重复执行

    loadedPaths.add(to.path);

    // 关闭页面加载进度条
    if (preferences.transition.progress) {
      stopProgress();
    }
  });
}

/**
 * 权限访问守卫配置
 * @param router
 */
function setupAccessGuard(router: Router) {
  router.beforeEach(async (to, from) => {
    const accessStore = useAccessStore();
    const userStore = useUserStore();
    const authStore = useAuthStore();

    // 基本路由，这些路由不需要进入权限拦截
    if (coreRouteNames.includes(to.name as string)) {
      if (to.path === LOGIN_PATH) {
        const searchParams = new URLSearchParams(window.location.search);
        const returnUrl = searchParams.get('returnUrl');
        const error = searchParams.get('error');

        // OIDC 流程失败时，清除可能过期的前端凭据，避免无限循环
        if (error && accessStore.accessToken) {
          accessStore.setAccessToken(null);
          accessStore.setRefreshToken(null);
          ElNotification({
            message: '登录会话已失效，请重新登录',
            title: '认证失败',
            type: 'warning',
          });
          return true;
        }

        // 有 accessToken 且有 OIDC returnUrl，尝试继续授权流程
        if (returnUrl && accessStore.accessToken) {
          window.location.href = returnUrl;
          return false;
        }

        if (accessStore.accessToken) {
          return decodeURIComponent(
            (to.query?.redirect as string) ||
              userStore.userInfo?.homePath ||
              preferences.app.defaultHomePath,
          );
        }
      }
      return true;
    }

    // accessToken 检查
    if (!accessStore.accessToken) {
      // 明确声明忽略权限访问权限，则可以访问
      if (to.meta.ignoreAccess) {
        return true;
      }

      // 没有访问权限，跳转登录页面
      if (to.path !== LOGIN_PATH) {
        return {
          path: LOGIN_PATH,
          // 如不需要，直接删除 query
          query:
            to.fullPath === preferences.app.defaultHomePath
              ? {}
              : { redirect: encodeURIComponent(to.fullPath) },
          // 携带当前跳转的页面，登录后重新跳转该页面
          replace: true,
        };
      }
      return to;
    }

    // 是否已经生成过动态路由
    if (accessStore.isAccessChecked) {
      return true;
    }

    // 生成路由表表
    // 当前登录用户拥有的权限码列表（包含角色和权限，支持通配符）
    let userInfo = userStore.userInfo;
    let accessCodes: string[] = [];
    if (!userInfo) {
      try {
        // 页面刷新时重新获取用户信息和权限码，确保数据最新
        const [fetchedUserInfo, fetchedAccessCodes] = await Promise.all([
          authStore.fetchUserInfo(),
          getAccessCodesApi(),
        ]);
        userInfo = fetchedUserInfo;
        accessCodes = fetchedAccessCodes;
        accessStore.setAccessCodes(accessCodes);
      } catch {
        // 拉取用户信息失败（token 失效、刷新竞态等）时不能让初始导航抛未捕获错误，
        // 否则路由启动直接崩溃、页面卡死；清理会话回登录页重新登录。
        await authStore.logout();
        return false;
      }
    } else {
      // userInfo 已缓存时，从 store 获取权限码
      accessCodes = accessStore.accessCodes;
    }

    // 生成菜单和路由
    // 传入 accessCodes（权限码列表）而非 roles（角色列表），以支持通配符匹配
    const { accessibleMenus, accessibleRoutes } = await generateAccess({
      roles: accessCodes,
      router,
      // 则会在菜单中显示，但是访问会被重定向到403
      routes: accessRoutes,
    });

    // 保存菜单信息和路由信息
    accessStore.setAccessMenus(accessibleMenus);
    accessStore.setAccessRoutes(accessibleRoutes);
    accessStore.setIsAccessChecked(true);
    const redirectPath = (from.query.redirect ??
      (to.path === preferences.app.defaultHomePath
        ? userInfo.homePath || preferences.app.defaultHomePath
        : to.fullPath)) as string;

    return {
      ...router.resolve(decodeURIComponent(redirectPath)),
      replace: true,
    };
  });
}

/**
 * 项目守卫配置
 * @param router
 */
function createRouterGuard(router: Router) {
  /** 通用 */
  setupCommonGuard(router);
  /** 权限访问 */
  setupAccessGuard(router);
}

export { createRouterGuard };

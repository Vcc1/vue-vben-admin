import type { Component } from 'vue';

import AlertRulesPanel from './AlertRulesPanel.vue';
import AppMetricsDashboardPanel from './apps/appMetrics/AppMetricsDashboardPanel.vue';
import ExpediaAppStatusPanel from './apps/expediaChaseRooms/ExpediaAppStatusPanel.vue';
import ExpediaEanepsDashboardPanel from './apps/expediaChaseRooms/ExpediaEanepsDashboardPanel.vue';
import ExpediaTaapDashboardPanel from './apps/expediaChaseRooms/ExpediaTaapDashboardPanel.vue';
import HotelMatchingDashboardPanel from './apps/hotelMatchingPipeline/HotelMatchingDashboardPanel.vue';
import HotelMatchingPipelineDashboardPanel from './apps/hotelMatchingPipeline/HotelMatchingPipelineDashboardPanel.vue';
import OTARoomBuilderDashboardPanel from './apps/otaRoomBuilder/OTARoomBuilderDashboardPanel.vue';
import SupStaticDataPanel from './apps/otaRoomBuilder/SupStaticDataPanel.vue';
import QueryPriceSupRoomBuilderToRedisDashboardPanel from './apps/queryPriceSupRoomBuilderToRedis/QueryPriceSupRoomBuilderToRedisDashboardPanel.vue';
import RoomMatchingStatisticsDashboardPanel from './apps/roomMatchingStatistics/RoomMatchingStatisticsDashboardPanel.vue';
import SupRoomInfoRedisToSqlDashboardPanel from './apps/supRoomInfoRedisToSql/SupRoomInfoRedisToSqlDashboardPanel.vue';
import BasicInfoPanel from './BasicInfoPanel.vue';
import LatestMetricsPanel from './LatestMetricsPanel.vue';
import RuntimeMetricsPanel from './RuntimeMetricsPanel.vue';

export type AppPanelKey =
  | 'alerts'
  | 'appMetricsDashboard'
  | 'appStatus'
  | 'basic'
  | 'expediaEanepsDashboard'
  | 'expediaTaapDashboard'
  | 'hotelMatchingDashboard'
  | 'hotelMatchingPipelineDashboard'
  | 'latest'
  | 'otaRoomBuilderDashboard'
  | 'queryPriceSupRoomBuilderToRedisDashboard'
  | 'roomMatchingStatisticsDashboard'
  | 'runtime'
  | 'supRoomInfoRedisToSqlDashboard'
  | 'supStaticDataDashboard';

export interface AppPanelDefinition {
  component: Component;
  key: AppPanelKey;
  label: string;
}

const panelDefinitions: Record<AppPanelKey, AppPanelDefinition> = {
  appMetricsDashboard: {
    component: AppMetricsDashboardPanel,
    key: 'appMetricsDashboard',
    label: 'AppMetrics',
  },
  appStatus: {
    component: ExpediaAppStatusPanel,
    key: 'appStatus',
    label: '应用状态',
  },
  alerts: {
    component: AlertRulesPanel,
    key: 'alerts',
    label: '告警/报表',
  },
  basic: {
    component: BasicInfoPanel,
    key: 'basic',
    label: '基础信息',
  },
  expediaEanepsDashboard: {
    component: ExpediaEanepsDashboardPanel,
    key: 'expediaEanepsDashboard',
    label: 'EANEPS 比价',
  },
  expediaTaapDashboard: {
    component: ExpediaTaapDashboardPanel,
    key: 'expediaTaapDashboard',
    label: 'Expedia TAAP',
  },
  hotelMatchingPipelineDashboard: {
    component: HotelMatchingPipelineDashboardPanel,
    key: 'hotelMatchingPipelineDashboard',
    label: '酒店清洗',
  },
  hotelMatchingDashboard: {
    component: HotelMatchingDashboardPanel,
    key: 'hotelMatchingDashboard',
    label: '酒店匹配',
  },
  latest: {
    component: LatestMetricsPanel,
    key: 'latest',
    label: '指标最新值',
  },
  otaRoomBuilderDashboard: {
    component: OTARoomBuilderDashboardPanel,
    key: 'otaRoomBuilderDashboard',
    label: 'OTA 建房',
  },
  supStaticDataDashboard: {
    component: SupStaticDataPanel,
    key: 'supStaticDataDashboard',
    label: 'Sup静态数据',
  },
  queryPriceSupRoomBuilderToRedisDashboard: {
    component: QueryPriceSupRoomBuilderToRedisDashboardPanel,
    key: 'queryPriceSupRoomBuilderToRedisDashboard',
    label: '查价建房',
  },
  roomMatchingStatisticsDashboard: {
    component: RoomMatchingStatisticsDashboardPanel,
    key: 'roomMatchingStatisticsDashboard',
    label: '映射统计',
  },
  runtime: {
    component: RuntimeMetricsPanel,
    key: 'runtime',
    label: '.NET Runtime / Process',
  },
  supRoomInfoRedisToSqlDashboard: {
    component: SupRoomInfoRedisToSqlDashboardPanel,
    key: 'supRoomInfoRedisToSqlDashboard',
    label: 'RedisToSql',
  },
};

export class DefaultAppPanelConfig {
  private readonly appPanels: Record<string, AppPanelKey[]> = {
    EanepsPriceComparisonWorker: [
      'expediaEanepsDashboard',
      'basic',
      'runtime',
      'latest',
    ],
    ExpediaChaseRooms: ['expediaTaapDashboard', 'basic', 'runtime', 'latest'],
    HotelMatchingPipeline: [
      'hotelMatchingDashboard',
      'hotelMatchingPipelineDashboard',
      'basic',
      'runtime',
      'latest',
    ],
    ConnectmateApi: ['appMetricsDashboard', 'basic', 'runtime', 'latest'],
    MengTuConnectmateApi: ['appMetricsDashboard', 'basic', 'runtime', 'latest'],
    OTARoomBuilder: [
      'otaRoomBuilderDashboard',
      'supStaticDataDashboard',
      'basic',
      'runtime',
      'latest',
    ],
    QueryPriceSupRoomBuilderToRedis: [
      'queryPriceSupRoomBuilderToRedisDashboard',
      'basic',
      'runtime',
      'latest',
    ],
    RoomMatching: ['appMetricsDashboard', 'basic', 'runtime', 'latest'],
    RoomMatchingStatistics: [
      'roomMatchingStatisticsDashboard',
      'basic',
      'runtime',
      'latest',
    ],
    SupRoomInfoRedisToSql: [
      'supRoomInfoRedisToSqlDashboard',
      'basic',
      'runtime',
      'latest',
    ],
  };

  private readonly defaultPanels: AppPanelKey[] = [
    'basic',
    'alerts',
    'runtime',
    'latest',
  ];

  getPanels(appName?: string) {
    const normalizedName = appName?.trim();
    const configuredPanelKeys = normalizedName
      ? this.appPanels[normalizedName] || this.defaultPanels
      : this.defaultPanels;
    const panelKeys: AppPanelKey[] =
      this.ensureAlertsPanel(configuredPanelKeys);
    return panelKeys.map((key) => {
      const definition = panelDefinitions[key];
      if (normalizedName === 'RoomMatching' && key === 'appMetricsDashboard') {
        return {
          ...definition,
          label: '房型匹配',
        };
      }
      return definition;
    });
  }

  private ensureAlertsPanel(panelKeys: AppPanelKey[]): AppPanelKey[] {
    if (panelKeys.includes('alerts')) {
      return panelKeys;
    }

    const basicIndex = panelKeys.indexOf('basic');
    if (basicIndex === -1) {
      return ['alerts' as AppPanelKey, ...panelKeys];
    }

    return [
      ...panelKeys.slice(0, basicIndex + 1),
      'alerts' as AppPanelKey,
      ...panelKeys.slice(basicIndex + 1),
    ];
  }
}

export const defaultAppPanelConfig = new DefaultAppPanelConfig();

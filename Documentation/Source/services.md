# 可执行服务

以下入口来自当前代码和 `AI_INDEX.md`。新增或重命名入口时必须同步本表。

| 服务 | 项目 | 说明 |
| --- | --- | --- |
| `MT-RoomMatching` | `Models/Room/RoomMatchings/Core/RoomMatching.Cli` | 房型匹配主流程 |
| `MT-OTARoomInfoCompletion` | `Models/Room/RoomMatchings/Core/OTARoomInfoCompletion.Cli` | OTA 房型信息补全 |
| `MT-SupRoomInfoRedisToSql` | `Models/Room/SupRoomInfo/Ingestion/SupRoomInfoRedisToSql.Cli` | 供应商房型缓存入库 |
| `MT-ExpediaChaseRooms` | `Models/Price/ExpediaChaseRoom/ExpediaChaseRooms.Cli` | Expedia 追价房型流程 |
| `MT-GrafanaAlertWebhook` | `Models/MetricsAndAlerts/GrafanaAlert/GrafanaAlertWebhook.Api` | Grafana 告警 Webhook |
| `MT-MetricsAndAlerts` | `Models/MetricsAndAlerts/MetricsAndAlerts/MetricsAndAlerts.Api` | 指标和告警 API |

## 入口定位

CLI 和 API 的 `Program.cs` 是运行时配置入口。处理业务问题时，先从本表定位入口项目，再沿 `.App`、`.Domain`、`.Infra` 和 Providers 的依赖方向阅读。

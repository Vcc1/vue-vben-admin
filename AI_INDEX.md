# MengtuV AI Index

> 给 AI/开发者一个“先看哪里”的入口，减少每次全仓库扫描。

## 1. Repository Overview

- Solution: `MengtuV.slnx`
- Main layout:
  - `Models/<大业务分组>/<具体业务>/<Domain|Application|Infrastructure|Presentation>`: business modules grouped by capability.
  - `Models/Base/Shared`: shared domain/application/infrastructure/presentation utilities.
  - `Providers`: external provider implementations for SQL, Redis, HTTP, Web, and supplier integrations.
  - `999.DLL`: local binary dependencies.

## 2. Quick Navigation

- Program entries: `Models/**/Presentation/**/Program.cs`
- Core room matching logic: `Models/RoomMatchings/Core/Application/RoomMatch.Core` and `Models/RoomMatchings/Core/Domain/Domain.RoomMatch`
- Shared OTA domain: `Models/Base/Shared/Domain/OTADomain`
- Provider implementations: `Providers/Infrastructure.*`
- TAAP/Expedia chain: `Models/Taap/ExpediaChaseRoom/**` plus `Providers/Infrastructure.Web.Taap`

## 3. Executable Services

- `Models/RoomMatchings/Core/Presentation/RoomMatching`
  ServiceName: `MT-RoomTypeMatchingTest`

- `Models/RoomMatchings/Core/Presentation/OTARoomInfoCompletion`
  ServiceName: `MT-OTARoomInfoCompletion`

- `Models/SupRoomInfo/Ingestion/Presentation/SupRoomInfoRedisToSql`
  ServiceName: `MT-SupRoomInfoRedisToSql`

- `Models/Taap/ExpediaChaseRoom/Presentation/ExpediaChaseRooms`
  ServiceName: `MT-ExpediaChaseRooms`

- `Models/RoomMatchings/Statistics/Presentation/RoomMappingSupStaticRoomInfoStatistics`
  RoomMapping static room-info statistics worker.

- `Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Presentation/QueryPriceSupRoomBuilderToRedis`
  Query-price supplier room builder entry.

## 4. Module Map

- Base shared:
  `Models/Base/Shared/Domain`, `Models/Base/Shared/Application`, `Models/Base/Shared/Infrastructure`, `Models/Base/Shared/Presentation`

- Room matching:
  `Models/RoomMatchings/Core`, `Models/RoomMatchings/OTARoomBuilder`, `Models/RoomMatchings/Statistics`

- Supplier room info:
  `Models/SupRoomInfo/Ingestion`

- Room data processing:
  `Models/RoomDataProcessings/QueryPriceSupRoomBuilder`

- Metrics and alerts:
  `Models/MetricsAndAlerts/GrafanaAlert`, `Models/MetricsAndAlerts/MetricsAndAlerts`

- TAAP:
  `Models/Taap/ExpediaChaseRoom`

## 5. Reading Order

- Room matching issue:
  `Models/RoomMatchings/Core/Presentation/RoomMatching`, then `Models/RoomMatchings/Core/Application/RoomMatch.Core`, then `Providers/Infrastructure.Sql.RoomMapping`.

- OTA room completion issue:
  `Models/RoomMatchings/Core/Presentation/OTARoomInfoCompletion`, then `Models/RoomMatchings/Core/Application/RoomMatch.Core`, then supplier/static-data providers.

- Expedia chase-room issue:
  `Models/Taap/ExpediaChaseRoom/Presentation/ExpediaChaseRooms`, then `Models/Taap/ExpediaChaseRoom/Application/ExpediaChaseRoomApp`, then `Providers/Infrastructure.Web.Taap` and `Providers/Infrastructure.Sql.SupCacheDB`.

## 6. Maintenance Notes

- Add new business code under `Models/<大业务分组>/<具体业务>/<layer>`.
- Add reusable provider integrations under `Providers`.
- Keep project names and namespaces stable unless doing a dedicated naming cleanup.

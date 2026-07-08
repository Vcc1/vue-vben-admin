# MengtuV AI Index

> Quick map for humans and agents: where to look first without scanning the whole repo.

## 1. Repository Overview

- Solution: `MengtuV.slnx`
- Main layout:
  - `Models/<business-group>/<business>/<project>`: business modules grouped by capability.
  - Project suffixes:
    - `.Domain`: domain projects.
    - `.App`: application projects.
    - `.Infra`: infrastructure projects.
    - `.Cli`, `.Web`, `.Api`: presentation projects by host type.
  - `Providers`: shared provider projects. Provider project names are intentionally unchanged.
  - `999.DLL`: local binary dependencies.

## 2. Quick Navigation

- Program entries: `Models/**/*.Cli/Program.cs`, `Models/**/*.Web/Program.cs`, `Models/**/*.Api/Program.cs`
- Core room matching logic: `Models/Room/RoomMatchings/Core/RoomMatch.Core.App` and `Models/Room/RoomMatchings/Core/RoomMatch.Domain`
- Shared OTA domain: `Models/Base/Shared/OTA.Domain`
- Provider implementations: `Providers/Infrastructure.*`
- TAAP/Expedia chain: `Models/Taap/ExpediaChaseRoom/**` plus `Providers/Infrastructure.Web.Taap`

## 3. Executable Services

- `Models/Room/RoomMatchings/Core/RoomMatching.Cli`
  ServiceName: `MT-RoomMatching`

- `Models/Room/RoomMatchings/Core/OTARoomInfoCompletion.Cli`
  ServiceName: `MT-OTARoomInfoCompletion`

- `Models/Room/SupRoomInfo/Ingestion/SupRoomInfoRedisToSql.Cli`
  ServiceName: `MT-SupRoomInfoRedisToSql`

- `Models/Taap/ExpediaChaseRoom/ExpediaChaseRooms.Cli`
  ServiceName: `MT-ExpediaChaseRooms`

- `Models/MetricsAndAlerts/GrafanaAlert/GrafanaAlertWebhook.Api`
  ServiceName: `MT-GrafanaAlertWebhook`

- `Models/MetricsAndAlerts/MetricsAndAlerts/MetricsAndAlerts.Api`
  ServiceName: `MT-MetricsAndAlerts`

- `Models/Hotel/SupplierStaticData/SupplierStaticDataAppTester.Web`
  ServiceName: `MT-SupplierStaticDataAppTester`

## 4. Module Map

- Base shared:
  `Models/Base/Shared`

- Room matching:
  `Models/Room/RoomMatchings/Core`, `Models/Room/RoomMatchings/OTARoomBuilder`, `Models/Room/RoomMatchings/Statistics`

- Supplier room info:
  `Models/Room/SupRoomInfo/Ingestion`

- Room data processing:
  `Models/Room/RoomDataProcessings/QueryPriceSupRoomBuilder`

- Metrics and alerts:
  `Models/MetricsAndAlerts/GrafanaAlert`, `Models/MetricsAndAlerts/MetricsAndAlerts`

- TAAP:
  `Models/Taap/ExpediaChaseRoom`

## 5. Reading Order

- Room matching issue:
  `Models/Room/RoomMatchings/Core/RoomMatching.Cli`, then `Models/Room/RoomMatchings/Core/RoomMatch.Core.App`, then `Providers/Infrastructure.Sql.RoomMapping`.

- OTA room completion issue:
  `Models/Room/RoomMatchings/Core/OTARoomInfoCompletion.Cli`, then `Models/Room/RoomMatchings/Core/RoomMatch.Core.App`, then supplier/static-data providers.

- Expedia chase-room issue:
  `Models/Taap/ExpediaChaseRoom/ExpediaChaseRooms.Cli`, then `Models/Taap/ExpediaChaseRoom/ExpediaChaseRoom.App`, then `Providers/Infrastructure.Web.Taap` and `Providers/Infrastructure.Sql.SupCacheDB`.

## 6. Maintenance Notes

- Add new business code directly under `Models/<business-group>/<business>/<ProjectName.Suffix>`.
- Do not recreate layer folders such as `Application`, `Infrastructure`, `Domain`, or `Presentation`.
- Keep provider project names under `Providers` unchanged unless doing a dedicated provider cleanup.
- Internal C# namespaces are intentionally not aligned with project names yet.

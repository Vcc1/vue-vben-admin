# MengtuV AI 索引（面向快速阅读/低 Token）

> 目的：给 AI/开发者一个“先看哪里”的入口，减少每次全仓扫描。

## 1) 仓库总览

- 解决方案：`MengtuV.slnx`
- 分层结构（按目录命名）：
  - `0.0VBaseFrameworks`：基础框架与服务默认配置
  - `1.Domain`：领域模型（OTA/RoomMatch）
  - `2.Infrastructure`：基础设施（SQL/Redis/HTTP/Web）
  - `3.Application`：应用层业务编排
  - `4.Presentation`：可执行入口（Worker/Console 服务）

## 2) 快速定位规则（建议 AI 首先使用）

- 要找“程序入口”：优先看 `4.Presentation/**/Program.cs`。
- 要找“核心房型匹配逻辑”：优先看 `3.Application/RoomMatchings/RoomMatch.Core`，再跳 `1.Domain/Domain.RoomMatch`。
- 要找“数据库上下文/持久化”：优先看 `2.Infrastructure/Infrastructure.Sql.*`。
- 要找“Redis 相关”：优先看 `2.Infrastructure/Infrastructure.Redis.*`。
- 要找“TAAP/Expedia 抓取链路”：先看 `4.Presentation/Taap/ExpediaChaseRooms`，再看 `3.Application/Taap/ExpediaChaseRoomApp` 与 `2.Infrastructure/Infrastructure.Web.Taap`。

## 3) 可执行项目（运行入口）

> 这些项目通常最能反映“系统实际做什么”。

1. `4.Presentation/RoomMatchings/RoomMatching`
   - ServiceName: `MT-RoomTypeMatchingTest`
   - 作用：房型匹配主流程入口，初始化多套 DB 配置并注册 RoomMatch 业务。

2. `4.Presentation/RoomMatchings/OTARoomInfoCompletion`
   - ServiceName: `MT-OTARoomInfoCompletion`
   - 作用：OTA 房型信息补全任务入口，依赖 `RoomMatch.Core` + API/RoomMapping/静态数据上下文。

3. `4.Presentation/RoomMatchings/SupRoomInfoRedisToSql`
   - ServiceName: `MT-SupRoomInfoRedisToSql`
   - 作用：供应商房型数据 Redis -> SQL 的同步/处理入口。

4. `4.Presentation/Taap/ExpediaChaseRooms`
   - ServiceName: `MT-ExpediaChaseRooms`
   - 作用：TAAP Expedia 抓取任务入口，组合 App 层与 SupCacheDB。

5. `4.Presentation/DataStatistics/RoomMappingSupStaticRoomInfoStatistics`
   - 作用：RoomMapping 相关统计 Worker（Host + DbContext + HostedService）。

6. `4.Presentation/RoomDataProcessings/SupRoomInfoRedisToSql`
   - 当前状态：模板/占位（`Hello, World!`）。

## 4) 分层项目索引（按职责）

### 4.1 Framework 层（0.0VBaseFrameworks）

- `VBaseFramework.Core`
  - 通用能力：EF Core、配置、日志、Redis、Cron 等基础组件。
- `VBaseFramework.ServiceDefaults`
  - 服务化默认装配：HTTP Resilience、ServiceDiscovery、Swagger、WindowsService 等。

### 4.2 Domain 层（1.Domain）

- `OTADomain`
  - OTA 相关领域对象（纯领域基础）。
- `Domain.RoomMatch`
  - 房型匹配领域对象/词典相关核心模型。

### 4.3 Infrastructure 层（2.Infrastructure）

- SQL：
  - `Infrastructure.Sql.MengTuAPI`
  - `Infrastructure.Sql.MengTuSup`
  - `Infrastructure.Sql.MengTuYDOTA`
  - `Infrastructure.Sql.MixedData`
  - `Infrastructure.Sql.RoomMapping`
  - `Infrastructure.Sql.SupCacheDB`
- Redis：
  - `Infrastructure.Redis.RoomMatchingRedis`
  - `Infrastructure.Redis.SupPriceCahce`
  - `Infrastructure.Redis.SupRoomInfo`
- HTTP/Web：
  - `Infrastructure.HTTP.SupStaticData`
  - `Infrastructure.Web.Taap`（包含 Playwright 依赖）

### 4.4 Application 层（3.Application）

- `RoomMatch.Core`
  - 房型匹配应用核心（聚合 Domain + 多个 SQL 基础设施项目）。
- `ExpediaChaseRoomApp`
  - TAAP Expedia 抓取应用编排层（连接 Web/Redis/SQL）。

## 5) 主要依赖关系（简化图）

- `Presentation.RoomMatching*` -> `Application.RoomMatch.Core` -> `Domain.RoomMatch + OTADomain` -> `Infrastructure.Sql.*`
- `Presentation.OTARoomInfoCompletion` -> `Application.RoomMatch.Core + Infrastructure.HTTP.SupStaticData + Infrastructure.Sql.*`
- `Presentation.ExpediaChaseRooms` -> `Application.ExpediaChaseRoomApp` -> `Infrastructure.Web.Taap + Redis/SQL`

## 6) 建议 AI 阅读顺序（最省 Token）

### 场景 A：排查“房型匹配结果异常”
1. `4.Presentation/RoomMatchings/RoomMatching/Program.cs`
2. `4.Presentation/RoomMatchings/RoomMatching/Worker*`
3. `3.Application/RoomMatchings/RoomMatch.Core` 下相关 Service/Analyzer
4. `1.Domain/Domain.RoomMatch` 的实体/词典定义
5. `2.Infrastructure/Infrastructure.Sql.RoomMapping` 与 `Infrastructure.Sql.MengTuSup`

### 场景 B：排查“OTA 信息补全任务失败”
1. `4.Presentation/RoomMatchings/OTARoomInfoCompletion/Program.cs`
2. 同目录 `Worker*`
3. `3.Application/RoomMatchings/RoomMatch.Core`
4. `2.Infrastructure/Infrastructure.HTTP.SupStaticData`
5. `2.Infrastructure/Infrastructure.Sql.MengTuAPI` / `Infrastructure.Sql.RoomMapping`

### 场景 C：排查“Expedia 抓取任务异常”
1. `4.Presentation/Taap/ExpediaChaseRooms/Program.cs`
2. `3.Application/Taap/ExpediaChaseRoomApp`
3. `2.Infrastructure/Infrastructure.Web.Taap`
4. `2.Infrastructure/Infrastructure.Sql.SupCacheDB`

## 7) 维护建议（给未来 AI/开发者）

- 新增可执行服务时：在本文件“第 3 节”补一条入口说明（ServiceName + 作用 + 上游依赖）。
- 新增基础设施项目时：在“第 4.3 节”登记类别（SQL/Redis/HTTP/Web）。
- 若目录职责发生变化：同步更新“第 2 节快速定位规则”。


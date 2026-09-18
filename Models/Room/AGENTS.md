# Models/Room — Agent Instructions

## Domain Scope

Room 域负责供应商房型数据的构建、匹配与持久化，是平台将供应商原始房型映射为 OTA 标准房型的核心管线。

## Sub-module Layout

```
Models/Room/
├── RoomMatchings/                 ← 房型匹配核心
│   ├── Core/
│   │   ├── RoomMatch.Domain/      ← 匹配算法、RoomContext、模式词典
│   │   ├── RoomMatch.Core.App/    ← 匹配编排（EBKRoomCreateApp 等）
│   │   ├── RoomMatch.Infra/       ← 匹配数据访问
│   │   ├── RoomMatching.Cli/      ← 匹配批处理 CLI
│   │   └── OTARoomInfoCompletion.Cli/  ← OTA 房型信息补全管线
│   ├── OTARoomBuilder/            ← OTA 房型构建器
│   │   ├── OTARoomBuilder.App/    ← 构建编排 + ISupplierRoomBuildPlugin 插件体系
│   │   ├── OTARoomBuilder.Infra/  ← 构建数据访问（OtaRoomBuilderRepository）
│   │   └── OTARoomBuilder.Cli/    ← 构建 CLI 入口
│   └── Statistics/                ← 匹配统计 CLI 工具
├── RoomDataProcessings/           ← 房型数据处理
│   └── QueryPriceSupRoomBuilder/  ← 查价场景的 SupRoom 构建
│       ├── QueryPriceSupRoomBuilder.App/    ← 构建管线（Pipeline + Steps）
│       ├── QueryPriceSupRoomBuilder.Infra/  ← Redis/SQL 持久化
│       └── QueryPriceSupRoomBuilderToRedis.Cli/
└── SupRoomInfo/                   ← 供应商房型信息
    └── Ingestion/                 ← Redis → SQL 批量写入
        ├── SupRoomInfoIngestion.App/
        ├── SupRoomInfoIngestion.Infra/
        └── SupRoomInfoRedisToSql.Cli/
```

## Key Patterns

### Plugin Architecture (OTARoomBuilder)

- `ISupplierRoomBuildPlugin<TMatcher>` 定义供应商房型构建插件契约。
- `BaseSupplierRoomBuildPlugin<TMatcher>` 提供通用默认实现。
- 各供应商（Ean / StaticData / WordRule / StrictEqual）继承基类实现特定匹配策略。
- 插件通过 `ServiceAttribute` + `ServiceCollectionExpand.AddBusiness()` 自动发现注册。
- 使用 `RoomContext` / `SupplierRoomBuildContext` 在插件间传递数据。

### Pipeline Pattern (QueryPriceSupRoomBuilder)

- 构建任务拆分为独立 Step 类：`RoomBuildTaskReaderStep` → `RoomBuildTaskQueryPriceStep` → `RoomBuildTaskPersistenceStep`。
- `RoomBuildTaskPipeline` 串联各 Step，`RoomBuildTaskWorker` 驱动整体执行。
- `TriggerQueueWorker` 从 Redis 队列消费触发信号。
- 每个 Step 只做一件事，不在 Step 间传递去重元数据。

### Contracts

- `IQueryPriceSupRoomBuildTaskRepository` — 任务读写
- `ISupRoomRedisPublisher` — Redis 发布
- `ITriggerQueueClient` / `IQueryPriceClient` — 队列与查价网关
- `IOtaRoomBuilderRepository` — OTA 房型构建数据访问

## Conventions Specific to Room

1. **处理步骤拆分**：每个管线步骤独立为一个类，不要在单个方法中串联多步逻辑。
2. **去重边界**：去重判断在读取/过滤阶段完成，不向下游写/持久化阶段传播去重元数据。
3. **数据复用**：`OTARoomInfo.Extension` 已在工作项上加载时，禁止在 `SaveParsedHotelBatchTask` 等下游重新从数据库读取。
4. **指标日志**：管线中按阶段逐行记录指标（队列剩余 - 进行中 - 已完成），包含各步骤计数。
5. **插件扩展**：新增供应商匹配策略时，继承 `BaseSupplierRoomBuildPlugin` 并在对应 `.Infra` 项目中注册，不要修改核心匹配引擎。

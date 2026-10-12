# Models/Hotel — Agent Instructions

## Domain Scope

Hotel 域负责供应商酒店数据的采集、清洗、匹配、业务分析与 OTA 同步，是平台酒店维度数据从原始入库到标准化输出的完整链路。

## Sub-module Layout

```
Models/Hotel/
├── HotelMatching/                     ← 酒店匹配核心
│   └── MatchingPipeline/
│       ├── HotelMatchingPipeline.Domain/     ← 领域模型（RawHotelData, HotelCleaningResult, HotelMatchStatus）
│       │   ├── Cleaning/                     ← CoordinateNormalization, CountryCodeNormalizer
│       │   └── Hotels/                       ← 酒店实体定义
│       ├── HotelMatchingPipeline.App/        ← 匹配编排
│       │   ├── Cleaning/                     ← HotelCleaningService（清洗管线）
│       │   │   ├── Configuration/            ← HotelCleaningOptions, HotelStarLevelOptions
│       │   │   └── Contracts/                ← IHotelCleaningRepository, IHotelStarLevelNormalizer
│       │   └── Matching/                     ← HotelMatchingService（匹配引擎）
│       │       ├── Configuration/            ← HotelMatchingOptions, HotelMatchVetoOptions
│       │       ├── Contracts/                ← IHotelMatchCache, IHotelMatchDelivery, IHotelMatchingRepository
│       │       └── Models/                   ← HotelDebugAnalysis, HotelMatchVetoModels
│       ├── HotelMatchingPipeline.Infra/      ← Redis + SQL 实现 + InMemory 测试替身
│       ├── HotelMatchingPipeline.Api/        ← REST 控制器
│       ├── HotelMatchingPipeline.Cli/        ← 批处理 CLI（HotelMatchingPipelineWorker）
│       └── HotelMatchingPipeline.AgentTools/ ← AI Agent 工具贡献器
├── HotelBusiness/                     ← 酒店业务分析
│   ├── HotelBusiness.Contracts/       ← 跨模块 DTO（HotelBusinessRunSummary 等）
│   ├── HotelBusiness.App/             ← HotelBusinessApp 编排
│   ├── HotelBusiness.Api/             ← REST 端点
│   └── HotelBusiness.Infra/           ← 文件存储（FileHotelBusinessStore）
├── HotelResearch/                     ← 酒店调研（AI 驱动）
│   ├── HotelResearch.Contracts/       ← IHotelResearchService 等契约
│   ├── HotelResearch.App/             ← HotelResearchService（调用 AgentPlatform）
│   ├── HotelResearch.Api/             ← REST 端点
│   ├── HotelResearch.Client/          ← HTTP 客户端封装
│   └── HotelResearch.Infra/           ← 种子数据读取、电话标准化
├── SupplierStaticData/                ← 供应商静态数据
│   └── SupplierStaticData.App/        ← ISupplierStaticDataProvider 插件体系
└── OTAHotelInfoSync/                  ← OTA 酒店信息同步
    ├── OTAHotelInfoSync.App/          ← 同步编排
    ├── OTAHotelInfoSync.Infra/        ← OTA HTTP + SQL + 企微通知
    └── OTAHotelInfoSync.Cli/          ← 同步 CLI 入口
```

## Key Patterns

### Two-Phase Pipeline: Cleaning → Matching

酒店匹配分为两个严格分离的阶段：

1. **Cleaning（清洗）**：`HotelCleaningService` 对 `RawHotelData` 执行坐标归一化、国家代码标准化、星级归一化，产出 `HotelCleaningResult`。
2. **Matching（匹配）**：`HotelMatchingService` 将清洗后的酒店与 MTHotelCode 进行匹配，产出 `HotelMatchStatus`。

两个阶段各有独立的 Options、Contracts、Metrics 和 Repository 接口，不要混合关注点。

### Veto Mechanism

- `IHotelMatchVetoRepository` / `IHotelMatchVetoRepository` 提供匹配否决能力。
- `HotelMatchVetoRedisSyncWorker` 将否决规则从 SQL 同步到 Redis 缓存。
- `HotelMatchVetoService` 提供否决查询 API。
- 否决规则在匹配前检查，命中的酒店直接跳过匹配流程。

### Match Delivery

- 匹配结果通过 `IHotelMatchDelivery` 投递到下游系统。
- `HotelMatchDeliveryCommand` 包含幂等键（IdempotencyKey），支持重试。
- 投递状态流转：创建 → Sending → Completed（含 nextRetryTime）。

### AI-Driven Research

- `HotelResearchService` 通过 `IAgentService`（AgentPlatform）调用 AI Agent 进行酒店调研。
- 使用 `IHotelResearchSeedReader` 读取种子数据，`IHotelBusinessStore` 读写业务分析结果。
- `IHotelOperationConcurrencyGuard` 确保同一酒店不会并发执行互斥操作。
- `HotelBusinessOperations.HotelResearch` 常量标识操作类型。

### Supplier Static Data Plugin

- `ISupplierStaticDataProvider` 按 `SupId` 区分供应商。
- `Supports(SupplierStaticDataKind)` 决定供应商支持的数据类型。
- 新增供应商只需实现该接口并在 DI 中注册。

## Conventions Specific to Hotel

1. **清洗与匹配分离**：Cleaning 和 Matching 各有独立的 Service、Options、Contracts、Metrics，修改一个阶段不应影响另一个。
2. **幂等投递**：匹配结果投递必须使用 IdempotencyKey，确保重试安全。
3. **否决优先**：匹配前必须检查 Veto 规则，命中则跳过，不要将否决逻辑嵌入匹配引擎内部。
4. **并发保护**：对同一 MTHotelCode 的写操作必须通过 `IHotelOperationConcurrencyGuard` 串行化。
5. **InMemory 测试替身**：`HotelMatchingPipeline.Infra/Testing/` 提供 `InMemoryHotelMatchingStore` 等实现，测试时优先使用。
6. **Agent 工具扩展**：为 AI Agent 添加酒店域工具时，实现 `IAgentToolContributor` 并在 `HotelMatchingPipeline.AgentTools` 中注册。

# MengtuV 项目文档

本目录是 MengtuV 的唯一文档源。文档同时面向人和 AI：内容使用稳定的 Markdown 标题层级，示例和路径保持可复制，关键约束写成明确规则。`catalog.json` 负责项目、功能、文档和资源的结构化目录；驱动只展示已经生成到本目录的内容，不读取源码或自动扫描 `.csproj`。

## 文档结构

- `Projects/<project-id>/index.md`：按项目归档的项目说明，每个项目单独一个目录。
- `architecture.md`：解决方案分组、项目边界和依赖方向。
- `development.md`：新增项目、新功能和文档同步流程。
- `services.md`：可执行服务、入口和运行时信息。

## 阅读入口

先阅读架构，再根据工作内容查看服务清单和开发约定。AI 需要定位代码时，优先从本文件和 `AI_INDEX.md` 开始，然后沿模块路径进入具体项目。

生成站点提供四种目录视图：总览、Models 层级 SVG 图、按项目物理路径、按功能；`SVG 与资源` 页面会把所有 SVG 按原始比例全宽展示。驱动只读取本目录已经生成好的文档，不扫描源码或项目文件。

## 生成文档

在仓库根目录运行：

```text
dotnet run --project Documentation/Documentation.Driver -- --clean
```

驱动会扫描 `Documentation/Source`，把每个 Markdown 文件渲染为 `Documentation/site` 下的 HTML，并生成 `manifest.json` 供工具快速索引。

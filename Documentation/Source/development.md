# 开发与文档同步

## 新增项目

1. 在正确的物理目录下创建项目，目录名不要带数字。
2. 在 `MengtuV.slnx` 中把项目放入带数字的解决方案分组。
3. 在本目录更新 `architecture.md` 的分组或项目说明。
4. 如果项目有运行入口，再更新 `services.md` 和根目录 `AI_INDEX.md`。
5. 运行文档驱动，检查目录、HTML 和 `manifest.json`。

项目和功能说明写入 `Documentation/Source/catalog.json`。每个项目使用以下字段：`id`、`name`、`path`、`summary`、`features`、`documents`；功能使用 `id`、`name`、`summary`，可选 `document` 或 `asset` 指向具体说明或资源。`path` 使用仓库相对路径，物理目录不带解决方案编号。

项目文档统一放在 `Documentation/Source/Projects/<project-id>/` 下，入口文件命名为 `index.md`。`catalog.json` 中的 `documents` 必须指向这个入口文件；驱动会按该路径生成对应的站点目录。

```json
{
  "projects": [
    {
      "id": "room-matching",
      "name": "Room Matching",
      "path": "Models/Room/RoomMatchings/Core/RoomMatching.Cli/RoomMatching.Cli.csproj",
      "summary": "房型匹配主流程。",
      "features": [
        { "id": "match", "name": "匹配", "summary": "执行 OTA 与供应商房型匹配。", "document": "room-matching.md" }
      ],
      "documents": ["room-matching.md"]
    }
  ]
}
```

## 新增功能

功能完成后，在最接近的模块文档中补充用途、入口、关键数据流和验证方式。跨模块功能要同时更新架构依赖和服务清单；不能只把说明写在提交信息里。

## 写作约定

- 每个源文件只有一个 `#` 标题，使用 `##`、`###` 表达层级。
- 路径、类型名、命令和配置键使用反引号包裹。
- 先写结论和边界，再写实现细节；避免只描述代码当前长什么样。
- 变更行为时说明兼容性、失败处理和验证命令。
- 面向 AI 的信息必须可定位到文件或项目；面向人的说明补充背景和操作上下文。

## 验证命令

```text
dotnet build Documentation/Documentation.Driver/Documentation.Driver.csproj
dotnet run --project Documentation/Documentation.Driver -- --clean
```

生成成功的标志是控制台显示文档数量，且 `Documentation/site/index.html`、各页面和 `manifest.json` 均存在。

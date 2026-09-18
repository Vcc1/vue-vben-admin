# Documentation

本目录分为两部分：

- `Documentation.Driver`：.NET 10 文档驱动程序，只扫描已经生成到 `Source` 的文档、`catalog.json` 和资源文件，生成目录、排版 HTML 和写出 `manifest.json`；SVG/图片会在目录中预览，其他文件可直接打开或下载。
- `Source`：文档源文件，只在这里编辑内容。`catalog.json` 是项目/功能说明的指定格式，资源可放在任意子目录。

项目说明按 `Source/Projects/<project-id>/index.md` 存储；公共架构和开发规范放在 `Source` 根目录。驱动会保留这个目录层级生成 HTML，项目文档不会和公共文档混在同一层。

生成产物写入 `site`，可用浏览器直接打开 `site/index.html`。21:9 宽屏会使用更宽的内容栅格，普通屏幕和移动端会自动收窄。从仓库根目录执行：

```text
dotnet run --project Documentation/Documentation.Driver -- --clean
```

生成页面还包括 `site/models-structure.html`（Models 层级 SVG 图）、`site/catalog-by-path.html`（按项目路径）、`site/catalog-by-feature.html`（按功能）和 `site/assets.html`（资源图库）。

解决方案登记使用 `/7.Documentation/`，物理目录仍为 `Documentation/`，与仓库的数字分组规则一致。

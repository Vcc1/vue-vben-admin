# Documentation.Driver

## 项目定位

这是 MengtuV 的文档展示项目。它只读取 `Documentation/Source` 中已经生成好的 Markdown、`catalog.json` 和资源文件，不扫描源码、项目文件或运行时依赖。

## 目录职责

- 读取项目结构和功能元数据。
- 生成总览、按路径、按功能和 Models SVG 结构图。
- 将 Markdown 转换为可导航的 HTML 页面。
- 将 SVG、图片、PDF 和其他资源复制到站点并提供访问入口。

## 生成命令

```text
dotnet run --project Documentation/Documentation.Driver -- --clean
```

输出位于 `Documentation/site`。项目文档使用 `Documentation/Source/Projects/<项目标识>/index.md` 目录格式，后续新增项目时沿用此结构，并在 `catalog.json` 的 `projects.documents` 中登记。

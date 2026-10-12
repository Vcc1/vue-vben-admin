# TablePage 横向滚动条吸底问题

## 目录现状
- 组件位置：`C:\Users\admin\.qoder-cn\worktrees\app\2caa65\MengtuV\UI\packages\effects\element-plus-ui\src\components\table-page\table-page.vue`
- 技术栈：Vue 3 + Element Plus + TypeScript
- 布局：ElCard 内三段式（工具栏 sticky top / 表格 / 分页栏 sticky bottom）

## 需求
让 ElTable 的横向滚动条始终浮动在视口底部，用户无需滚动到表格底部即可横向滚动。

## 已尝试方案

### 方案 1：CSS sticky 对 ElTable 自定义滚动条
```css
.table-page-card .el-table__body-wrapper .el-scrollbar__bar.is-horizontal {
  position: sticky;
  bottom: 0;
}
```
**结果**：不生效，滚动条没吸住

### 方案 2：el-table-horizontal-scroll 插件
```vue
<ElTable v-horizontal-scroll.always>
```
**结果**：滚动有延迟/不丝滑（插件用 JS 模拟滚动条，性能不如原生）

### 方案 3：自定义同步滚动条（当前方案）
- 隐藏原生滚动条（非 scoped CSS）
- 在分页栏上方放一个自定义滚动条容器
- JS rAF 轮询双向同步 scrollLeft
**问题**：复杂，且之前出现过双滚动条/同步失效问题

## 核心约束
1. **必须用原生滚动条**（性能/流畅度要求）
2. **不能隐藏原生滚动条再搞同步条**（之前踩过坑：ElTable 内部 JS 会导致 scrollLeft 被 clamp 回 0）
3. **滚动条要始终可见**（浮动在视口底部，不是表格底部）

## ElTable DOM 结构
```
.el-table
  .el-table__inner-wrapper
    .el-table__header-wrapper (表头)
    .el-table__body-wrapper (overflow: hidden)
      .el-scrollbar
        .el-scrollbar__wrap (实际滚动容器，overflow: auto，原生滚动条在这里)
        .el-scrollbar__bar (Element Plus 自定义滚动条轨道)
```

## 关键发现
- 原生滚动条在 `.el-scrollbar__wrap` 上
- `.el-table__body-wrapper` 是 `overflow: hidden`
- scoped CSS 对 `::-webkit-scrollbar` 不生效，必须用非 scoped 样式块
- 滚动容器是 `#__vben_layout_scroll`（Vben 布局），不是页面 body

## 请求
请提供一个方案，让原生横向滚动条始终浮动在视口底部，要求：
1. 用原生滚动条（流畅）
2. 不要 JS 同步（避免性能问题和 scrollLeft clamp）
3. 纯 CSS 或最小 JS

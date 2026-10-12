# UI Fork 分支策略

本仓库 (vue-vben-admin) 是 MengtuV 项目的 UI 框架 fork。

## 分支说明

| 分支 | 用途 |
|------|------|
| `main` | 稳定版本，定期同步上游 vbenjs/vue-vben-admin |
| `feature/*` | 新功能开发，在 fork 中维护，稳定后挑选提 PR 到上游 |
| `fix/*` | BUG 修复，可直接提 PR 到上游 |

## 工作流

1. **新功能开发**
   - 从 `main` 创建 `feature/xxx` 分支
   - 在 fork 中开发、测试、维护
   - 功能稳定后，挑选合适的功能提 PR 到上游

2. **BUG 修复**
   - 从 `main` 创建 `fix/xxx` 分支
   - 修复后直接提 PR 到上游

3. **同步上游**
   - 定期从 `vbenjs:main` 拉取更新
   - 在 fork 的 `main` 分支合并

## 当前功能分支

### feature/table-page-sticky-scroll
- **功能**: TablePage 组件重构 + 横向滚动条吸底
- **状态**: 已完成，在 MengtuV 项目中验证
- **包含**:
  - TablePage 通用组件（工具栏/密度/列配置）
  - CSS sticky 实现横向滚动条始终浮动在视口底部
  - 完整 TypeScript 泛型支持
- **PR 到上游**: 待功能稳定后评估

## 注意

- 新功能**不主动**合并到上游，保持 fork 独立性
- 备忘文档只存在于此 fork 仓库，不在 MengtuV 主仓库提交
- 挑选 PR 时确保功能通用、无业务耦合

# 解决方案架构

## 命名和物理路径

解决方案中的虚拟目录使用数字前缀表达阅读顺序；磁盘上的物理目录不带数字。当前分组如下：

| 解决方案分组 | 物理目录 | 职责 |
| --- | --- | --- |
| `0.Base` | `Models/Base`、`BaseFrameworks` | 共享领域和基础框架 |
| `1.Hotel` | `Models/Hotel` | 酒店资料、酒店业务和酒店匹配 |
| `2.Room` | `Models/Room` | 房型资料、房型匹配和房型处理 |
| `3.Price` | `Models/Price` | 价格查询、可用性和 Expedia 链路 |
| `4.MetricsAndAlerts` | `Models/MetricsAndAlerts` | 指标、告警和测试指标 |
| `5.AI` | `Models/AI` | Agent 平台及其扩展能力 |
| `6.Data` | `Models/Backoffice` | 后台和数据管理能力 |
| `7.Documentation` | `Documentation` | 文档源和文档渲染驱动 |

新增分组时，解决方案分组必须带下一个数字，物理目录保持语义名称，不要把数字写入磁盘路径。

## 项目层次

业务项目按能力目录组织，再按项目后缀表达职责：`.Domain` 放领域模型和规则，`.App` 放用例和应用编排，`.Infra` 放外部系统适配，`.Api`、`.Cli`、`.Web` 放入口层。测试项目放在被测项目下的 `Tests` 目录，并同时登记到解决方案的 `/Tests/` 虚拟目录。

## 依赖方向

入口层依赖应用层和基础设施层；应用层依赖领域层和抽象；基础设施层实现抽象并依赖 Providers。Providers 是共享适配器，项目名保持现状，除非进行专门的 provider 清理。

## 文档项目

`Documentation/Documentation.Driver` 不依赖业务项目，保证在业务代码无法编译时仍可生成文档。`Documentation/Source` 是人工维护的源文件；`Documentation/site` 和 `manifest.json` 是可重建产物。

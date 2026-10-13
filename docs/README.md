# 文档入口

当前阶段为功能收敛和定版前验收。先看运行与集成说明，再根据工作内容查规范；历史任务、评审和原型用于追溯，不当作当前版本的验收结论。

## 使用与交付

| 文档 | 内容 |
| --- | --- |
| [根 README](../README.md) | 当前能力、环境要求、快速启动 |
| [定版核对清单](release-readiness.md) | 当前版本、开发验证和现场待验收项 |
| [SDK 用法](../client/src/LabelService.Client/README.md) | 生成、批量集合、错误处理、字段来源 |
| [Windows 静默打印](../client/src/LabelService.Client.Windows/README.md) | USB/驱动打印、PDF、ZPL、单项与子集 |
| [部署](../backend/deploy/README.md) | Docker、Windows 服务、数据库、备份恢复 |
| [测试](testing.md) | 统一验证、可选浏览器回归、客户端自检、压测 |

## 维护规范

| 文档 | 内容 |
| --- | --- |
| [AGENTS](../AGENTS.md) | 所有维护者的工作流程和硬性规则 |
| [契约](contracts.md) | 模板 JSON、开放接口、错误码、头、SDK 行为 |
| [管理接口](admin-api.md) | 登录、权限、模板、素材、系统信息 |
| [架构](architecture.md) | 三个部分、后端分层、缓存和扩展点 |
| [性能](performance.md) | 指标口径、预算、基准与历史测量 |
| [ADR](adr/README.md) | 已采纳的架构决定及理由 |

契约权威为后端 Contracts；运行命令和版本以实际项目文件、代码为准。发现文档不一致时同步文档，不能仅修改说明就宣称功能或性能已实现。

## 设计与历史

- [设计书](design/label-service-design.html)：已评审方案，只同步实现，不擅自重做设计。
- [网页原型](prototypes/label-designer-prototype.html)、[客户端原型](prototypes/label-sdk-demo-prototype.html)：设计参考，当前页面以运行产品为准。
- [任务索引](tasks/README.md)：范围、负责人、交接和待验收状态；不因准备定版自动把历史任务全部标成完成。
- [历史评审入口](reviews/README.md)：对应提交的检查结论和证据。
- [后续候选](roadmap.md)：未承诺的二期方向，从主使用入口分离，避免把已实现功能列成待开发。

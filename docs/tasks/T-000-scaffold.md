# T-000 搭建仓库骨架

状态：已完成
负责人：Claude（Claude Code）
里程碑：—
依赖：—
设计书：全部

## 目标

搭好一条能跑通的最小链路（walking skeleton），让后续任务都是“往已有结构里填实现”，并把协作规则写成文档和自动检查。

## 范围

- 仓库结构：前端 frontend、后端 backend、调用端 client 三个独立部分（ADR 0004），各自的解决方案和编译配置；全仓库共用的 .editorconfig、global.json、verify.ps1
- Contracts：模板 JSON 模型、协议常量、请求和响应体
- Rendering：绑定、字段校验、计时、渲染流水线；线、矩形两种元素；PNG 输出
- Server：单张生成、字段定义接口、API Key 鉴权、文件模板存储、指标
- Client：完整公开 API、重试、错误映射、打印辅助类
- WinForms 最小演示、Vue 设计器骨架、检查程序、基准
- README、AGENTS.md、docs（架构、契约、性能、ADR、任务）

## 共享文件

- 全部（骨架阶段）

## 验收标准

- [x] 后端、调用端编译 0 警告 0 错误
- [x] 后端、调用端检查程序全部通过（含 SDK 对真实服务的端到端检查）
- [x] 单张 PNG 接口端到端可用（只画外框和横线）
- [x] 设计器 `npm run build` 通过，能显示和拖动示例模板
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 文本、条码、图片等元素的渲染，PDF、ZPL 输出（见后续任务）

## 交接记录

- 2026-10-09 · Claude · 按要求把仓库拆成 frontend、backend、client 三个部分，各自独立的解决方案和检查程序；跨部分的契约一致性检查放在后端检查程序里，SDK 端到端检查由 verify.ps1 启动服务后执行（ADR 0004）。
- 2026-10-09 · Claude · 完成骨架。几个设计书没写明、骨架阶段先定下的约定，列在 docs/contracts.md 的“骨架阶段补充的约定”里，实现对应任务时可以提出修改。设计书和两个原型从工作目录移到了 docs/design、docs/prototypes。

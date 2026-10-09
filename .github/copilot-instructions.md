# Copilot 说明

本仓库给所有 AI 助手的说明统一写在仓库根目录的 `AGENTS.md`，动手前请完整阅读。最重要的几条：

- 仓库分成 `frontend/`（Vue 设计器）、`backend/`（契约、渲染、服务端）、`client/`（SDK、演示程序）三个部分，互不引用项目和文件。
- 每项工作对应 `docs/tasks/` 下的一个任务文件，只改任务范围内的文件。
- 契约（模板 JSON、错误码、HTTP 头）的权威定义在 `backend/src/LabelService.Contracts`；改契约要同时改 `client/` 和 `frontend/` 里的副本和 `docs/contracts.md`。
- 后端依赖方向：Contracts ← Rendering ← Server；`client/src/LabelService.Client` 必须能编译到 net462。
- 渲染热路径不访问数据库、磁盘、网络；单张 P95 ≤ 100 ms。
- 注释和文档用中文，标识符和提交信息用英文。
- 交付前通过：`powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1`（只改一个部分可加 `-Part backend`、`-Part client` 或 `-Part frontend`）。

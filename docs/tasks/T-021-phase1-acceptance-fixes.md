# T-021 一期验收问题修复与总体 review

状态：进行中
负责人：Claude 2026-10-10
里程碑：一期验收
依赖：T-001～T-019
设计书：§9 生成性能与监控、§8 开放接口、§4 A4 拼版；ADR 0006、0007、0009

## 目标

按独立验收（Codex，2026-10-10）指出的一期问题逐项修复、补需求缺口和验证，再做一期最终总体 review。验收材料在仓库外（`C:\Users\dukun\Documents\Codex\2026-10-09\task\acceptance`），这里只记结论、命令和本仓库里的证据。

## 范围

可以新建、修改的文件：

- `backend/tests/LabelService.Benchmarks/`：门禁 fail-closed、恢复完整基准覆盖（不改既有基线、不放宽门槛）
- `backend/src/LabelService.Server/Endpoints/`：真实计时口径（到写完最后一个字节）
- `backend/src/LabelService.Rendering/`：PDF 中文 ToUnicode（复制、搜索出来的字要对）；单张性能
- `backend/src/LabelService.Server/Data/`、`Templates/`：旧库升级的数据转换、迁移前备份和失败恢复
- `frontend/src/`：拼版设计器缺口（模板详情的可用版式）、真实交互验证
- 后端、前端、调用端的检查程序；`docs/`：如实记录验证结果和未决问题；`docs/reviews/`：总体 review 记录

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `AGENTS.md`（第 1 节当前阶段）
- `docs/tasks/README.md`（任务索引追加一行）
- `docs/performance.md`、`docs/contracts.md`（计时口径、实测结果）

## 验收标准

- [ ] 基准门禁 fail-closed：构建失败、0 项执行、基线项缺席都不通过；恢复拼版 PNG 基准；不改基线重跑全门禁
- [ ] 调用日志、指标的总耗时算到写完最后一个字节，有独立验证
- [ ] PDF 里中文的 ToUnicode 正确（日、寸、毛等不再变成康熙部首），有检查
- [ ] 旧库（AddApiClients 版本）升级不丢模板分类和调用方授权；迁移前自动备份，中途失败能恢复，有故障注入验证
- [ ] 单张 50 并发持续压测：如实测量；达不到时给出证据和需要的决定，不改门槛
- [ ] 拼版设计器：模板详情列出可用版式；真实鼠标拖拽的交互验证
- [ ] ADR 0007、0009 等未采纳事项如实标注，列出需要用户决定的最小问题
- [ ] 一期总体 review 记录（权限、错误处理、任务缺口、测试有效性）
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 二期 T-020；推送 GitHub；安装证书、凭据、新服务；真实打印机和生产数据；修改既有性能基线或放宽门槛

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

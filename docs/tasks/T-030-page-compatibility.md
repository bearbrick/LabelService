# T-030 固定最新版本的页面与恢复修复兼容整合

状态：进行中
认领人：Codex 2026-10-11
里程碑：一期开发
依赖：原 T-025 至 T-028，以及隔离页面候选 1b5ec3a
设计稿：沿用最新原 UI 与已验收页面修复；不新增功能

## 目标与范围

固定原 main 3168830，在隔离分支合入 1b5ec3a 的互补页面、源码登记、SQLite 恢复与请求竞态修复。保留新品牌、侧栏拖宽、用户页面反馈和字体实现，原目录不改。

范围为候选相对 087aeba 的已有变更，冲突协调可涉及 frontend/src/components/admin/TopBar.vue、TemplateTree.vue、frontend/src/admin.css、layout-recovery.css、main.ts 及相关测试；backend/src/LabelService.Infrastructure/Persistence/DatabaseUpgrade.cs 与已有恢复/登记检查。原 Rendering、Contracts、调用端、新系统信息/字体实现和用户新 UI 不重写。

共享 docs/tasks/README.md 仅保留并追加记录，单独提交。为解决编号重名，将隔离功能整合的 T-026 改为 T-031，隔离布局整合 T-028 改为 T-032，历史 Git 提交号和截图目录不变。相关任务/审阅文档链接和 README 可同步。合并 backend/tests/LabelService.Backend.Checks/Program.cs 时保留双方检查。

## 验收标准

- [ ] 来源稳定、固定提交，原目录只读
- [ ] 已授权互补修复与最新品牌/侧栏/页面反馈均保留
- [ ] 编号及内容冲突解决，无新增产品范围
- [ ] 必要实际浏览器兼容回归通过
- [ ] 干净克隆完整 verify.ps1 通过，无源码补拷贝
- [ ] 自建进程清理，手测数据保留
- [ ] 提交、完整证据、可安全快进或独立修复分支及回滚方案明确

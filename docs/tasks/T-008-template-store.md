# T-008 模板存储、版本发布与缓存

状态：待认领
负责人：—
里程碑：M3
依赖：—
设计书：§3 数据模型、§9 怎么做到；ADR 0003

## 目标

画布预设、模板、版本、素材元数据存进 SQLite；草稿和发布分开；开放接口从内存缓存取已发布版本。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Data/`：EF Core DbContext、实体、迁移（表结构按设计书第 3 节，库文件 `label.db`）；连接初始化统一设置 PRAGMA（见 ADR 0003）
- 新建 `backend/src/LabelService.Server/Templates/DbTemplateStore.cs`：启动时把全部已发布版本加载到内存，发布时刷新
- 保留 `FileTemplateStore`，用配置切换：本地没有数据目录时、检查程序的部分用例仍可用示例模板
- 首次启动时如果库是空的，把 `backend/samples/templates` 导入为已发布版本（开发环境）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/Directory.Packages.props`：`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.EntityFrameworkCore.Design`（生成迁移用）
- `backend/src/LabelService.Server/Program.cs`、`appsettings*.json`（新增 `LabelService:Data:Directory`）

## 验收标准

- [ ] 数据目录可配置，默认程序目录下 `data/`；启动时自动建库、执行迁移
- [ ] 时间列存 UTC `DateTime`，不用 `DateTimeOffset`（ADR 0003 的 Provider 限制）
- [ ] 发布时校验模板：绑定只引用已定义字段、条码旋转是直角、用样例值试渲染一次并计时（> 50 ms 给提示，不阻止发布）
- [ ] 发布后的版本内容不可修改；同一模板同一时间最多一个草稿
- [ ] 开放接口取模板不访问数据库（检查程序用计数或替身验证）
- [ ] 发布新版本后立即生效（单实例，不需要跨实例同步）
- [ ] 检查程序用临时数据目录，跑完删除
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 管理界面（见 T-014）
- 多实例缓存同步（ADR 0003：前期单实例）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 数据库已定为 SQLite、单实例部署（ADR 0003），状态改为待认领。
- 2026-10-09 · Claude · 标为阻塞：等数据库选型。确定后把状态改为待认领。

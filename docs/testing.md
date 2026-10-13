# 测试与验证入口

测试按能力和运行条件区分，不按“哪个 AI 写的”区分。保留能够验证产品行为的检查，清理重复说明和一次性产物；历史截图、旧检查项数不能代替当前候选验证。

## 默认交付检查

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1
```

| 检查 | 范围 | 运行条件 |
| --- | --- | --- |
| 文档本地链接 | Git 中及新增 Markdown 的本地文件目标；检查器自检 | Node、Git；不访问外部网站或验证锚点 |
| 后端 | 编译、格式、注释、契约、分层、接口、渲染、迁移与备份恢复 | .NET SDK 10.0.100+，开发思源字体 |
| SDK / Windows 扩展 | 编译各目标、格式、注释、协议、打印数据检查 | Windows 桌面运行时支持相关检查 |
| SDK 端到端 | 临时后端、独立数据目录、真实 HTTP 请求 | 脚本在 5089 端口启动，结束后清理 |
| 前端 | 117 项当前离线回归、注释与规模、TypeScript、生产构建 | Node 22.18+ 或 24，已安装前端依赖 |

默认验证不访问物理打印机、不发送 ZPL、不重置用户密码、不读写用户当前数据目录；后端检查使用隔离数据。运行中的本地 API 或客户端可能锁住编译输出，需先结束自己启动的进程，验证后恢复。不要停止不明来源的进程。

只改一个部分时可用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Part backend
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Part client
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Part frontend
node tools/check-docs.mjs
node tools/check-docs.mjs --self-test
```

文档或跨部分修改仍跑全量。源码中的 XML/JSDoc 检查保留，文档文件链接检查不替代它们。检查项数随新增回归变化，不把数字本身作为验收条件。

## 需要专门环境的检查

| 检查 | 入口 | 注意 |
| --- | --- | --- |
| 真实浏览器边界、导航/缩放、分段按钮 | [前端测试说明](../frontend/tests/README.md) | 已安装的 Playwright/Chrome、显式隔离服务及合成数据；不自动装依赖，不加入默认 check |
| 品牌组件回退 | 开发前端 `/tests/brand-mark.html` | 加载实际 Vue 组件，仅读公开系统信息，四种配置与 5 项 DOM 检查，不保存配置 |
| WinForms smoke | [调用端说明](../client/README.md) | 桌面会话，net48 与 net10.0-windows；输出到本次独立目录 |
| SQL Server / PostgreSQL | [后端说明](../backend/README.md) | `LABEL_TEST_SQLSERVER` / `LABEL_TEST_POSTGRES` 指向可创建临时库的专用环境 |
| Release 渲染基准 | [性能说明](performance.md) | 同规格、空闲机器，准备字体；计时结果和 HTTP P95 分开记录 |
| k6 现场压测 | [压测说明](../backend/tests/load/README.md) | 隔离/验收实例，不向日常服务无意发起高负载 |
| 驱动、实际纸张、扫码和出纸 | [定版清单](release-readiness.md) | 实施配置与真实设备，软件提交成功不代表打印机完成 |

浏览器 viewport、`deviceScaleFactor`、CSS zoom 都不能代替 Windows 原生 DPI 验收。浏览器自动化脚本只覆盖其明确列出的流程，不能宣称全部产品页面通过。

## 输出与保留

- 单次日志、截图、生成代码、SDK 包放到 `artifacts/<工作>/` 等 Git 忽略目录，不放源码根目录。
- 有价值的验收证据在 [历史评审](reviews/README.md) 中注明提交、日期、环境及限制；不删除唯一证据，不把旧结果改成新测量。
- 纯模型、接口、打印选择、字体和字段来源等实际回归保留在对应 tests 项目。
- 需要重录基准时单独评审并提交，不能为让门禁通过而改基线；当前已知异常见 [性能说明](performance.md)。

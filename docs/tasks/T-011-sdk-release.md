# T-011 客户端 SDK 发布

状态：待认领
负责人：—
里程碑：M2 出预览版，M4 出 1.0
依赖：T-007（批量接口端到端）
设计书：§10 客户端 SDK

## 目标

`LabelService.Client` 能以 NuGet 包和 zip 两种方式交给第三方，文档、签名、版本齐全。

## 范围

可以新建、修改的文件：

- `client/src/LabelService.Client/`：补 README.md（快速开始、错误码、常见问题）、CHANGELOG.md、包元数据、强名称签名配置
- 新建 `client/build/pack.ps1`：打 nupkg 和 zip（三个目标框架的 DLL、XML 注释、README、演示程序源码）

## 共享文件

- `client/Directory.Build.props`：SDK 的版本号策略（调用端独立于后端发版）

## 验收标准

- [ ] `dotnet pack` 生成的包包含 net462、netstandard2.0、net8.0 和 XML 注释
- [ ] 在只装 .NET Framework 4.8 的项目里引用包，设计书第 10 节的用法代码能编译运行
- [ ] 强名称签名；Authenticode 签名步骤写进脚本（证书由谁提供见设计书第 12 节）
- [ ] 公开 API 与设计书第 10 节一致；有变化时同步设计书
- [ ] 版本号遵循语义化版本，预览版带 `-preview.N`
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- Java、Python SDK

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

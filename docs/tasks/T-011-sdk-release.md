# T-011 客户端 SDK 发布

状态：待验收
负责人：Claude 2026-10-10
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

- [x] `dotnet pack` 生成的包包含 net462、netstandard2.0、net8.0 和 XML 注释
- [x] 在只装 .NET Framework 4.8 的项目里引用包，设计书第 10 节的用法代码能编译运行
- [x] 强名称签名；Authenticode 签名步骤写进脚本（证书由谁提供见设计书第 12 节）
- [ ] 公开 API 与设计书第 10 节一致；有变化时同步设计书（除了拼版的 Layout、StartSlot、PageCount，随 T-018 加）
- [x] 版本号遵循语义化版本，预览版带 `-preview.N`
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- Java、Python SDK

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-10 · Claude · 完成，待验收（正式签名要等证书和密钥）。
  - 包：版本改成 `1.0.0-preview.1`（`client/Directory.Build.props` 的 VersionPrefix + VersionSuffix，SDK 独立于后端发版）；包元数据（Apache-2.0、README、snupkg 符号包）；`src/LabelService.Client/README.md`（快速开始、错误处理和错误码表、选项、日志和对账、常见问题）和 `CHANGELOG.md` 打进包里。
  - 签名：强名称密钥、代码签名证书都不进仓库（R7，.gitignore 已排除 *.snk、*.pfx）。`-p:LabelServiceStrongNameKey=密钥` 时做强名称签名，签名构建去掉给检查程序的 InternalsVisibleTo。用临时生成的测试密钥打过一次包，三个目标框架的 DLL 都带 PublicKeyToken。Authenticode：脚本用 signtool 给 DLL 签名（SHA256 + 时间戳）、`dotnet nuget sign` 给 nupkg 签名，证书密码走环境变量；没有证书，这一步没实际跑过。
  - `client/build/pack.ps1`：编译 → （可选）Authenticode → dotnet pack → zip（三个框架的 DLL 和 XML、README、CHANGELOG、依赖说明、演示程序源码）→ 核对 nupkg 内容。输出到 `artifacts/client`（已忽略）。
  - 验证：新建 net48 控制台项目（C# 7.3），从本地目录引用 nupkg，跑设计书第 10 节的用法（单张 PDF、批量 ZPL、FIELD_REQUIRED 的逐条错误），对本机服务运行正常。
  - 设计书第 10 节 `LabelFile` 一行补上已经有的 `IsZip`、`RequestId`（同步修改）。拼版的 `Layout`、`StartSlot`、`PageCount` 还没有，等 T-018。
  - 待定：私有 NuGet 源用哪个、证书由谁申请（设计书第 12 节）。

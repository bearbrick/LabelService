# T-028 字体由客户上传

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-027
设计书：§render（字体）、ADR 0011

## 目标

客户在“素材与字体”页上传自己有授权的字体，上传后设计器和服务端渲染立即可用，不用重启；发布包不再带字体，避免分发授权问题（ADR 0011）。

## 范围

可以新建、修改的文件：

- 后端：`backend/src/LabelService.Rendering/Fonts/FontRegistry.cs`（重新加载）、`backend/src/LabelService.Application/Resources/`（字体应用服务）、`backend/src/LabelService.Infrastructure/`（字体目录、加载）、`backend/src/LabelService.Api/Endpoints/`（上传、删除、fonts.css）、`backend/src/LabelService.Api/LabelService.Api.csproj`（发布包不带字体）、检查程序
- 前端：`frontend/src/model/fonts.ts`、`frontend/src/api/resources.ts`、`frontend/src/stores/`、`frontend/src/pages/AssetsPage.vue`、`frontend/src/components/props/TextProps.vue`、`frontend/src/components/CanvasWorkspace.vue`、`frontend/tests/checks.ts`
- 做的过程中补充（见交接记录）：`backend/deploy/Dockerfile`（镜像不再要求字体）、`backend/tests/LabelService.Backend.Checks/Program.cs`（登记新检查）、`docs/reviews/phase1-review.md`（第 7 节第 10 条）
- 文档：`docs/adr/0011-customer-uploaded-fonts.md`、`docs/adr/README.md`、`docs/admin-api.md`、`docs/contracts.md`（fontFamily 说明）、`backend/README.md`、`backend/assets/fonts/README.md`、`backend/deploy/` 的说明、`frontend/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行
- `docs/contracts.md`：fontFamily 的说明（行为不变，补充“可以用上传的字体”）
- `AGENTS.md`：R7、第 1 节补一句

## 验收标准

- [x] “素材与字体”页可以上传字体（.ttf / .otf / .ttc / .otc，≤ 50 MB），列出每个文件的字体名、大小、来源（上传 / 部署目录），可以删除上传的
- [x] 上传后不用重启：设计器字体下拉框能选到、画布用同一份字体显示；服务端渲染用得上；删除后引用它的模板降级到默认字体（FONT_FALLBACK 警告）
- [x] 没有默认字体（思源黑体）时页面提示上传
- [x] 不是字体的文件、超大文件、和已有文件重名都拒绝；没有 res.manage 不能上传、删除；操作日志
- [x] 发布包不带字体文件（开发构建照旧复制）
- [x] 改了 Rendering：基准对比写进交接记录
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 字体子集化、按需下载（设计器整份下载字体文件，和原来一样）
- 指定“默认字体”（仍然是思源黑体）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-12 · Claude

**做了什么**（用户 2026-10-11：“字体希望可以通过外部上传使用，分发时不会有版权问题，让客户自己传字体上来”，决定写在 ADR 0011）：

- 渲染：`FontRegistry` 改成内部持有一份只读的 `FontSnapshot`，`Reload()` 时整份替换；渲染时读一次当前快照，换下来的旧快照等注册表释放时再释放（可能还有请求在用，不能立刻释放）。字体来自两个目录：部署目录（程序目录 `fonts/`）和上传目录（数据目录 `fonts/`）；上传的字体整份读进内存，不占着文件，Windows 上删除不会因为文件被占用失败。新增 `TemplateNames`（模板里可写的字体名）、`FileDetails`（每个文件的家族名、大小、来源）。
- 接口：`POST /api/admin/fonts`（res.manage，≤ 50 MB，这个接口单独把请求体上限放宽到 51 MB；按内容检查是不是字体；和已有文件重名拒绝）、`DELETE /api/admin/fonts/{file}`（只能删上传的）；`GET /fonts` 增加 `items`、`templateFamilies`、`hasDefaultFamily`；`fonts.css` 列出全部字体。上传、删除记操作日志（类型“字体”）。
- 发布：`LabelService.Api.csproj` 里字体改成 `CopyToPublishDirectory="Never"`，发布包和 Docker 镜像不带字体（Dockerfile 去掉了“没有字体就构建失败”的检查）；开发构建照旧复制，检查程序和基准不受影响。部署说明、Nginx 示例（字体上传单独放宽 `client_max_body_size`）、`backend/assets/fonts/README.md` 已更新。
- 前端：“素材与字体”页的字体页签可以上传、删除（删除前确认，说明会降级），列出字体名、模板里写的名字、预览、文件和大小、来源；没有思源黑体时提示上传；超过 50 MB 的文件在浏览器里先拦下（服务端会直接断开连接，浏览器只会报“连不上”）。设计器的字体下拉框改成服务端给的列表（内置两种在前，服务端没有的标“未安装”，模板正在用但服务端没有的也列出来）；上传、删除后重新加载 fonts.css，画布立即用新字体。

**基准**（`RenderBenchmarks`，改前 → 改后，同一台机器、同一天）：PNG 203 dpi 1.197 → 1.208 ms，PNG 300 dpi 2.394 → 2.325 ms，PDF 203 dpi 4.448 → 4.389 ms，PDF 300 dpi 4.524 → 4.390 ms，ZPL 203 dpi 1.420 → 1.391 ms，ZPL 300 dpi 2.896 → 2.848 ms；分配内存不变。热路径只多一次读引用，在误差范围内。

**验证**：后端检查 470 项通过（新增 8 项：空部署目录启动、上传思源黑体后生成不再降级、fonts.css 和字体文件、重名 / 不是字体 / 扩展名不对拒绝、权限、删除后文件删掉且又降级、操作日志）；前端检查 74 项（新增 3 项：字体名对应、CSS 降级、下拉框选项）、类型检查通过。用真实的 Kestrel 服务（部署字体目录为空）实测：上传 16.9 MB 的思源黑体 424 ms、24.0 MB 的思源宋体 637 ms（超过全局 20 MB 上限，说明这个接口的单独上限生效），52 MB 的文件被服务端拒绝；浏览器里上传后的列表、删除确认、设计器下拉框“思源宋体（未安装）”都正常。`verify.ps1` 全量通过（后端 470、调用端 47、前端 74）。

**留给用户**：发布包不带字体这一条是我按“分发时不会有版权问题”理解的，思源黑体、思源宋体本身可以随包分发；如果想继续随包带这两个、只是允许另外上传，改回 `CopyToPublishDirectory="PreserveNewest"` 即可（review 第 7 节第 10 条）。每上传或删除一次字体，旧的一份字体数据要到服务重启才释放内存（上传很少，问题不大）。

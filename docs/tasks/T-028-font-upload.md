# T-028 字体由客户上传

状态：进行中
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
- 文档：`docs/adr/0011-customer-uploaded-fonts.md`、`docs/adr/README.md`、`docs/admin-api.md`、`docs/contracts.md`（fontFamily 说明）、`backend/README.md`、`backend/assets/fonts/README.md`、`backend/deploy/` 的说明、`frontend/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行
- `docs/contracts.md`：fontFamily 的说明（行为不变，补充“可以用上传的字体”）
- `AGENTS.md`：R7、第 1 节补一句

## 验收标准

- [ ] “素材与字体”页可以上传字体（.ttf / .otf / .ttc / .otc，≤ 50 MB），列出每个文件的字体名、大小、来源（上传 / 部署目录），可以删除上传的
- [ ] 上传后不用重启：设计器字体下拉框能选到、画布用同一份字体显示；服务端渲染用得上；删除后引用它的模板降级到默认字体（FONT_FALLBACK 警告）
- [ ] 没有默认字体（思源黑体）时页面提示上传
- [ ] 不是字体的文件、超大文件、和已有文件重名都拒绝；没有 res.manage 不能上传、删除；操作日志
- [ ] 发布包不带字体文件（开发构建照旧复制）
- [ ] 改了 Rendering：基准对比写进交接记录
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新

## 不做

- 字体子集化、按需下载（设计器整份下载字体文件，和原来一样）
- 指定“默认字体”（仍然是思源黑体）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

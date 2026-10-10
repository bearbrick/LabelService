# T-016 服务端预览与字体下载

状态：待验收
负责人：Claude 2026-10-09
里程碑：M3
依赖：T-001（字体注册表）
设计书：§4 保证所见即所得、§7 字体、§8 接口列表

## 目标

设计器能用服务端渲染引擎出真实 PNG 预览，并加载和服务端同一份字体，保证所见即所得。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Endpoints/PreviewEndpoints.cs`：`POST /api/v1/preview`（未保存的模板 JSON + 样例数据 → PNG，需要登录）
- `GET /fonts/{name}`：返回字体文件，带长缓存头
- `frontend/src/`：“服务端预览”按钮，`@font-face` 加载服务端字体

## 共享文件

- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`：去掉预览的 501 占位（ADR 0007 采纳前先只改提示消息）
- `backend/src/LabelService.Server/Program.cs`：映射 /fonts
- `docs/contracts.md`：预览一行、二维码编码规则
- `AGENTS.md`：第 1 节

## 验收标准

- [x] 预览请求的模板不经过存储，直接校验后渲染；模板 JSON 非法时返回明确的错误位置
- [x] 预览不计入开放接口指标（用单独的 endpoint 维度）
- [x] 同一模板，设计器画布与服务端 PNG 的元素位置偏差 ≤ 0.2 mm（写一个对比脚本或检查）
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 登录本身（见 T-014，这里可以先用开发期开关）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-11 · Claude（T-022）· 用户确认采纳 ADR 0007：`/api/v1/preview` 的 501 占位已删除（现在是 404，有检查），设计书第 8 节和 contracts.md 已同步；预览只在 `POST /api/admin/preview`，`/fonts` 公开。
- 2026-10-10 · Claude（T-021 验收复核）· 如实补充：任务范围和设计书第 8 节写的是 `POST /api/v1/preview`（需要登录），实际做在 `POST /api/admin/preview`，`/api/v1/preview` 返回 501 指向管理接口。原因是已采纳的 ADR 0006 规定开放接口只认 API Key、登录令牌只在 /api/admin 有效（T-014 验收也要求登录态不能调开放接口），和“/api/v1 下需要登录的接口”冲突；解决办法写在 ADR 0007，**仍是“提议”，没有被批准**，所以公共合同没有改。需要用户决定：①采纳 ADR 0007（预览在管理接口，删掉 /api/v1/preview 的 501，同步设计书第 8 节和 contracts.md）；或②保留 /api/v1/preview 并让它例外接受登录令牌（要改 ADR 0006）。/fonts 公开也在 ADR 0007 里，同样待确认。
- 2026-10-09 · Claude · 完成，待验收。
  - 地址改了，请确认 ADR 0007：设计书第 8 节写的是 `POST /api/v1/preview`（需要登录），但 ADR 0006 已经定了登录令牌只在 `/api/admin` 有效。预览实现成管理接口 `POST /api/admin/preview`（tpl.view），`/api/v1/preview` 仍返回 501，消息指向新地址；ADR 采纳后再删掉它、改设计书和 contracts.md。
  - 预览：请求体 `{ template, data?, dpi?, mono? }`，模板不经过存储；JSON 不合法时 400，消息写 JSON 路径和行列（如“位置 $.template.elements[0]，第 1 行第 136 个字节”）；结构问题（元素 ID 重复、未定义的字段等，和发布检查同一套规则，但不要求编码合法）逐条列在 details；数据不合法、渲染失败也是 400。指标只记 endpoint=preview 的请求数和总耗时（`LabelMetrics.PreviewFinished`），不记阶段耗时和张数；不写调用日志。
  - 字体：`GET /fonts/fonts.css`、`GET /fonts/{文件名}`（不需要登录，@font-face 带不了令牌；字体是 OFL）。`FontRegistry.SourceOf` 按模板字体名找到渲染实际用的文件和度量；css 用 ascent-override / descent-override 把浏览器的度量设成服务端的值。设计器在 index.html 引入，vite 开发代理加了 /fonts。
  - 位置对比：工作区“服务端预览”里“和画布对比”（按模板 DPI），画布按服务端图片的像素尺寸导出（`editor/snapshot.ts`），逐个元素量墨迹边界的差（`editor/compare.ts`，有检查）。示例箱标 300 dpi：最大 0.085 mm（1 个点）。
  - 对比时发现并修了两处不一致：① 文本在画布上低 0.17～0.3 mm：画布单位是 mm，字号只有几个“像素”，浏览器的 fontBoundingBoxAscent/Descent 取整到整像素，Konva 用它定基线；改成文字按 100 倍字号排版再缩小（`draw.ts` 的 `TEXT_SCALE`）。② 二维码大一圈：bwip-js 用混合模式最小编码，ZXing 默认整段字节模式，示例内容 bwip-js 是版本 2、ZXing 是版本 3；服务端打开 ZXing 的 `QR_COMPACT`，两边一致（改了 Rendering，基准见 performance.md：耗时在误差内，每次多分配约 50 KB；新增基准 `BoxLabelPngUniqueBox`，每次换箱号、条码缓存不命中）。
  - 检查：后端新增 `PreviewChecks.cs`（10 项：预览尺寸和响应头、指标维度、传数据、JSON 错误位置、结构错误、API Key 不能调、fonts.css、字体文件和缓存头、只提供已加载的文件、字体注册表找文件）和 QR 最小编码 1 项；前端 compare 2 项。
  - 没做的：一个家族有多个字体文件（如静态的常规和粗体分开）时 fonts.css 只声明第一个文件，浏览器会合成粗体；现在用的是可变字体，不受影响。.ttc / .otc 字体集合浏览器不支持，跳过。

# 契约：模板 JSON 与开放接口

本文是设计器、服务端、SDK 之间的约定。**以代码为准**：模板模型在 `backend/src/LabelService.Contracts/Templates/`，协议常量在 `backend/src/LabelService.Contracts/Protocol/`；设计书第 5、6、8 节是叙述版本。三者不一致时，按“代码 → 本文 → 设计书”的顺序确定，并把落后的一方改掉。

改动契约前先读 AGENTS.md 的“契约变更”。检查程序会核对：本文列全了错误码、元素类型和 HTTP 头；SDK 的错误码和 HTTP 头与服务端一致；设计器的 TypeScript 模型包含全部元素类型。

## 1. 模板 JSON

### 通用约定

- 长度单位一律是 **mm**，保留 1 位小数；字号是 **pt**（1pt ≈ 0.353mm）；颜色是 `#RRGGBB`。
- 坐标原点在标签左上角，x 向右、y 向下。
- `rotation` 单位是度，**顺时针、绕元素中心**旋转。条码只允许 0、90、180、270。
- `elements` 数组顺序就是叠放顺序，后面的在上层。`hidden: true` 的元素不渲染；`locked` 只影响设计器。
- 属性名 camelCase；枚举写成字符串（见下表）；`type` 可以不在元素的第一个属性；未知的元素 `type` 解析失败。
- 省略的属性取默认值，设计器不必把默认值写进 JSON。
- `schemaVersion` 当前是 `1.0`。

### 顶层结构

```json
{
  "schemaVersion": "1.0",
  "code": "BOX_LABEL",
  "name": "成品箱标",
  "page": { "width": 100, "height": 60, "dpi": 300 },
  "fields": [ ... ],
  "elements": [ ... ]
}
```

完整示例见 `backend/samples/templates/box-label.json`。

### 字段定义 `fields[]`

| 属性 | 默认 | 说明 |
| --- | --- | --- |
| `key` | 必填 | 字母开头，只含字母、数字、下划线；模板内唯一，区分大小写 |
| `type` | `string` | `string` / `number` / `date` / `image` |
| `name` | — | 中文显示名 |
| `required` | `true` | 必填字段没传且没有默认值时返回 `FIELD_REQUIRED` |
| `default` | — | 没传时用的值 |
| `format` | — | number、date 的默认显示格式（.NET 格式字符串） |
| `sample` | — | 样例值，用于预览和 JSON 样例 |
| `maxLength` | — | string 的最大字符数，超出返回 `FIELD_TYPE_INVALID` |

数据校验规则（`FieldValidator`）：

- `null`、空字符串、全空白字符串都视为“没传”。
- `string`：接受字符串；数字和布尔值按原文转成字符串；对象、数组报 `FIELD_TYPE_INVALID`。
- `number`：接受 JSON 数字，或能按 InvariantCulture 解析成 decimal 的字符串。
- `date`：ISO 8601，接受 `yyyy-MM-dd`、`yyyy-MM-ddTHH:mm[:ss[.fffffff]]`、`yyyy-MM-dd HH:mm[:ss]`，以及带 `Z` 或 `+08:00` 的写法。带时区的值**保留调用方写的钟面时间**，不换算到服务器时区。
- `image`：PNG 或 JPG 的 base64，可带 `data:image/png;base64,` 前缀，解码后不超过 1 MB，否则 `IMAGE_INVALID`。只收 base64，不支持 URL。
- 多传的 key 忽略。字符串里有非法 UTF-8 时报 `FIELD_TYPE_INVALID`。
- 批量时先校验全部条目，任何一条不通过就整批返回，错误明细带条目序号 `index`（从 0 开始），提示文字写“第 n 条”（从 1 开始）。

### 绑定

- 写法：`{{key}}`、`{{key:format}}`，花括号内允许空格。固定文字和绑定可以混写：`批次：{{batchNo}}`。
- 格式优先级：绑定里的格式 → 字段的 `format` → 默认格式。一律用 InvariantCulture。
- 默认格式：number 用最短表示；date 在时间为 0 点时是 `yyyy-MM-dd`，否则 `yyyy-MM-dd HH:mm:ss`。
- 格式字符串非法时退回默认格式，不报错。
- 可选字段没传时替换为空字符串；引用了未定义的字段时替换为空，并给出 `BINDING_UNKNOWN` 警告（保存模板时设计器应先拦住）。

### 元素

所有元素都有：`id`、`type`、`name`、`x`、`y`、`width`、`height`、`rotation`、`locked`、`hidden`。

| `type` | 专有属性 | 渲染进度 |
| --- | --- | --- |
| `text` | `content`；`style`（见下） | 已实现 |
| `barcode` | `symbology`（默认 code128）、`value`、`align`（默认 left）、`showText`（默认 true）、`textPosition`（bottom / top）、`textStyle`、`minModuleDots`（默认 2）、`quietZone` | 已实现 |
| `qrcode` | `symbology`（qr / datamatrix / pdf417，默认 qr）、`value`、`ecLevel`（L / M / Q / H，默认 M，仅 QR）、`quietZone` | 已实现 |
| `image` | `src`（`asset:素材ID` 或 `{{字段}}`）、`fit`（contain / cover / stretch，默认 contain）、`mono` | 已实现 |
| `icon` | `icon`（图标库路径，如 `gb191/fragile`）、`color` | 已实现 |
| `line` | `stroke`；height 为 0 是横线，width 为 0 是竖线，都不为 0 画左上到右下的斜线；线宽以线为中心 | 已实现 |
| `rect` | `stroke`、`fill`、`radius`；边框画在框**内侧**，外沿和元素框对齐 | 已实现 |

`style` / `textStyle`：`fontFamily`（默认 `SourceHanSansSC`）、`fontSize`（默认 10pt）、`bold`、`italic`、`underline`、`color`（默认 #000000）、`align`（left / center / right，默认 left）、`vAlign`（top / middle / bottom，默认 middle）、`fit`（none / wrap / shrink，默认 none）、`minFontSize`（shrink 的最小字号，默认 4pt）、`lineHeight`（默认 1.2）、`letterSpacing`（mm）。

文本排版：

- `fit: none` 不自动换行，超出框的部分裁掉；`wrap` 在框宽内换行，中文按字、英文按单词，行首不放结束标点、行尾不放开始标点，单词比框还宽时按字符断开，超出框高的部分裁掉；`shrink` 不自动换行，放不下时缩小字号，最小到 `minFontSize`，仍放不下就裁掉。
- 内容里的换行符 `
` 在三种模式下都换行。
- 行高 = 字号 × `lineHeight`，文字在行内垂直居中；`letterSpacing` 加在相邻两个字之间。
- 有内容被裁掉时给 `TEXT_CLIPPED` 警告；字体不存在时降级到思源黑体，给 `FONT_FALLBACK` 警告。
- 字体名比较时忽略大小写、空格、连字符、下划线；`SourceHanSansSC` 也认 Noto Sans SC（同一款字体），`SourceHanSerifSC` 同理。

一维码：

- 模块宽度 = floor((框宽 − 2 × `quietZone`) 的打印点数 ÷ 模块数)，每条、每个空都是整数个点；小于 `minModuleDots` 返回 `BARCODE_TOO_DENSE`，错误消息里给出需要的最小宽度。
- EAN-13、EAN-8、UPC-A、ITF-14 可以少写最后一位，自动补校验位；写全时核对校验位。内容不符合码制返回 `BARCODE_INVALID`。
- GS1-128 的 `value` 写成 `(应用标识符)数据` 的形式，如 `(01)06901234567892(10)ABC`；开头自动加 FNC1，变长数据后面自动加分隔符，人眼可读文字保留括号。
- 人眼可读文字：字号默认 8pt，占“字号 × 1.2”的高度，和条之间隔 0.12 个字号，在条码下方（`textPosition: top` 时在上方）居中；EAN、UPC 显示补上校验位后的完整号码。
- `rotation` 只能是 0、90、180、270，发布模板时拦截其他角度；渲染时按最近的直角处理。

二维码：

- 模块大小 = floor(min(框宽, 框高) 减去两侧 `quietZone` 后的打印点数 ÷ 模块数)，在框里居中；小于 2 点返回 `BARCODE_TOO_DENSE`，内容超出容量返回 `BARCODE_INVALID`。
- 内容含中文等非 ASCII 字符时按 UTF-8 编码并带 ECI 标识；纯 ASCII 不加 ECI。`ecLevel` 只对 QR 生效。
- QR 用混合模式最小编码（数字、字母数字、字节分段），同样的内容和设计器（bwip-js）选出同样的版本和尺寸（T-016）。
- DataMatrix 固定为正方形；PDF417 每行高 3 个模块。

图片和图标：

- `image.src` 写 `asset:素材ID` 时取素材库（内置素材在 `backend/assets/images`，上传的素材在数据目录的 `assets/`，文件名就是 ID），写 `{{字段}}` 时取 image 字段；素材不存在、图片解码失败返回 `IMAGE_INVALID`；可选的图片字段没传时不画。
- `fit`：contain 等比缩放后居中，cover 等比放大后居中裁掉多余部分，stretch 拉伸填满。`mono: true` 时用 Floyd–Steinberg 抖动成黑白。
- `icon.icon` 是图标库路径（如 `gb191/fragile`），图标拉伸画满元素框，`color` 着色（默认黑色，单色着色）；图标不存在返回 `IMAGE_INVALID`。
ZPL：整张标签栅格化成黑白位图，每张标签一个 `^XA … ^XZ`，`^PW`、`^LL` 是点数，份数用 `^PQ`，位图用 `^GFA` 加 ZPL 自带的 ASCII 压缩。`mono: true` 时 PNG 也输出纯黑白（文字、线条按亮度阈值 128，图片用抖动），和 ZPL 打出来的一致。

PDF：矢量输出，一张标签每份一页；页面尺寸是模板的 mm 尺寸，按输出 DPI 对齐到打印点（误差小于半个点）；字体以 Type3 方式嵌入，文字可以选中、搜索。已知限制：少数汉字和康熙部首共用字形（如“日”），复制出来是部首字符，打印、显示不受影响。

- SVG（图标、素材）加载前会清洗：禁止 DTD，去掉脚本、样式表、foreignObject、动画和 on* 事件属性，链接只保留文档内引用和 `data:image/` 内嵌图片，不会访问外部地址。

`stroke`：`width`（mm，默认 0.3）、`dash`（实段、空段长度 mm，如 `[1, 0.5]`；不设为实线）、`color`。

一维码码制写法：`code128`、`code39`、`ean13`、`ean8`、`upca`、`itf14`、`gs1-128`。

文本排版和一维码尺寸规则（模块宽度 = floor(框宽点数 ÷ 模块数)，小于 `minModuleDots` 返回 `BARCODE_TOO_DENSE`）见设计书第 5 节，实现时以设计书为准。

> 骨架阶段补充的约定（设计书没写明，实现对应任务时可以提出修改）：默认字号 10pt；`quietZone` 单位 mm、默认 0，表示框内留白；线元素宽高都不为 0 时画斜线；矩形边框画在框内侧。

## 2. 开放接口

| 方法 | 路径 | 说明 | 状态 |
| --- | --- | --- | --- |
| POST | `/api/v1/render` | 单张生成，成功时返回文件流 | PNG 可用；PDF、ZPL 见 T-005、T-006 |
| POST | `/api/v1/render/batch` | 批量生成，返回合并文件或 ZIP | 可用 |
| GET | `/api/v1/templates/{code}/schema` | 最新发布版本的字段定义和样例 | 可用 |
| POST | `/api/v1/preview` | 设计器用：未保存的模板 + 样例数据 → PNG（需要登录） | 返回 501：预览实现在管理接口 `POST /api/admin/preview`（ADR 0007，提议中，采纳后删掉这一行） |
| GET | `/health` | 存活检查 | 可用；就绪检查见 T-017 |

### 单张请求

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `templateCode` | 是 | 模板编码 |
| `version` | 否 | 不传则用最新发布版本 |
| `format` | 是 | `pdf` / `png` / `zpl`，忽略大小写 |
| `dpi` | 否 | 203 / 300 / 600；不传则用模板设置，模板也没有则 300 |
| `mono` | 否 | 仅 png 有效 |
| `copies` | 否 | 1–100，默认 1。**PNG 只能 1 份**，多份请用批量接口并选 zip |
| `data` | 是 | 字段数据 |

> 规划中（T-018，ADR 0005）：单张和批量请求增加可选参数 `layout`（拼版版式编码）、`startSlot`（起始格子），响应头增加 `X-Page-Count`，错误码增加 `LAYOUT_NOT_FOUND`、`LAYOUT_SIZE_MISMATCH`。实现时把正式规范写进本文，并删除这段说明。

批量请求多出 `output`（`merge` / `zip`，默认 merge；PNG 只能 zip）、`common`（公共字段，条目里同名字段覆盖它）、`items[]`（`data`、`copies`）。单次总张数（含份数）上限 500。

批量的输出：merge 是一个文件（PDF 每张每份一页，ZPL 每张一个 `^XA…^XZ`），顺序和 `items` 一致；zip 里每个条目一个文件，文件名 `模板编码_序号.扩展名`（序号从 001 开始），PDF、ZPL 的份数在文件里，PNG 一个文件只能放一张，所以每份一个文件。先校验全部条目，任何一条不通过整批返回；渲染时出错（如条码内容不符合码制）也带条目序号。`Server-Timing` 各阶段是全部张数的累计值，`X-Label-Count` 是总张数（含份数）。

### HTTP 头

| 头 | 方向 | 说明 |
| --- | --- | --- |
| `X-Api-Key` | 请求 | 调用方的 Key，服务端只存 SHA-256 |
| `X-Request-Id` | 请求 | 可选，调用方生成的请求号，原样写进调用日志（最长 64 字符）；SDK 每次调用自动生成 |
| `X-Template-Version` | 响应 | 实际使用的模板版本 |
| `X-Label-Count` | 响应 | 文件里的张数，含份数 |
| `X-Label-Dpi` | 响应 | 实际 DPI |
| `X-Trace-Id` | 响应 | 链路 ID（W3C traceparent），成功和失败都带 |
| `Server-Timing` | 响应 | 各阶段耗时，成功和失败都带，见下 |
| `X-Label-Warnings` | 响应 | 渲染警告，逗号分隔的 `CODE:detail`，detail 经过 URL 编码 |

成功响应另有 `Content-Type`（`image/png`、`application/pdf`、`text/plain; charset=utf-8`（ZPL）、`application/zip`）和 `Content-Disposition`（文件名 `模板编码_yyyyMMdd.扩展名`）。

### Server-Timing

格式：`auth;dur=0.3, tpl;dur=0.1, validate;dur=0.6, layout;dur=4.2, draw;dur=18.5, encode;dur=21.7, total;dur=46.1`。毫秒保留 1 位小数，已知阶段按 `auth`、`tpl`、`validate`、`layout`、`draw`、`encode`、`total` 的顺序输出，同名阶段累加（批量逐张累加），失败时只有走到的阶段。

### 警告码

| 警告 | detail |
| --- | --- |
| `FONT_FALLBACK` | 原字体名，已降级到思源黑体 |
| `TEXT_CLIPPED` | 元素 ID，文本超出框被裁掉 |
| `BINDING_UNKNOWN` | 字段 key，绑定引用了未定义的字段 |

### 错误响应

```json
{
  "code": "FIELD_REQUIRED",
  "message": "第 2 条缺少必填字段 boxNo",
  "errors": [ { "index": 1, "field": "boxNo", "code": "FIELD_REQUIRED", "message": "第 2 条缺少必填字段 boxNo" } ],
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

`code` 取第一个错误；`errors` 列出全部。单张请求的 `index` 省略。

| HTTP | code | 含义 |
| --- | --- | --- |
| 401 | `UNAUTHORIZED` | Key 缺失或无效 |
| 403 | `TEMPLATE_FORBIDDEN` | 这个 Key 无权使用该模板 |
| 404 | `TEMPLATE_NOT_FOUND` | 模板或版本不存在，或者还没有发布过 |
| 400 | `INVALID_JSON` | 请求体不是合法 JSON，或缺少 templateCode、data（请求体超过 20 MB 时为 413） |
| 400 | `FORMAT_INVALID` | format 不是 pdf、png、zpl |
| 400 | `DPI_INVALID` | dpi 不是 203、300、600 |
| 400 | `OUTPUT_INVALID` | output 不是 merge、zip，或 PNG 批量没用 zip |
| 400 | `COPIES_INVALID` | copies 不在 1–100，或 PNG 单张超过 1 份 |
| 400 | `FIELD_REQUIRED` | 缺少必填字段 |
| 400 | `FIELD_TYPE_INVALID` | 字段值的类型、格式或长度不对 |
| 400 | `BARCODE_INVALID` | 条码内容不符合码制 |
| 400 | `BARCODE_TOO_DENSE` | 内容太长，框内放不下能扫的条码 |
| 400 | `IMAGE_INVALID` | 图片解码失败或超过 1 MB |
| 413 | `BATCH_TOO_LARGE` | 单次总张数超过上限 |
| 500 | `RENDER_FAILED` | 渲染内部错误，凭 traceId 查日志 |

只在 SDK 里产生的错误码：

| code | 自动重试 | 说明 |
| --- | --- | --- |
| `NETWORK_ERROR` | 是 | 连不上、DNS 失败 |
| `TIMEOUT` | 是 | 超过客户端 Timeout |
| `SERVICE_UNAVAILABLE` | 是 | 服务返回 502、503、504（不管响应体是什么） |
| `UNEXPECTED_RESPONSE` | 否 | 服务返回了无法识别的响应，如代理的 HTML 错误页 |

开发期临时响应 `NOT_IMPLEMENTED`（HTTP 501）表示接口或格式还没实现，**不属于契约**，SDK 不定义它，1.0 前必须全部消失。

## 3. 客户端 SDK 行为

公开 API 和用法见设计书第 10 节和 `client/src/LabelService.Client`。要点：

- 网络错误、超时、502/503/504 自动重试 `MaxRetries` 次（默认 2），间隔 `RetryDelay × 第几次`（默认 0.5 秒、1 秒）。其余错误不重试。
- 本地预校验不发请求，直接抛 `ArgumentException`：模板编码为空、份数不在 1–100、PNG 单张多份、PNG 批量没用 Zip、批量为空、批量总张数超过 `MaxLabelsPerRequest`。
- 值转换：DateTime 只有日期时转 `yyyy-MM-dd`，否则 `yyyy-MM-ddTHH:mm:ss`；DateTimeOffset 带时区；byte[] 转 base64；枚举转名称；null 不发送。

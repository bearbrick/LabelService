/**
 * 模板 JSON 的 TypeScript 定义，和 backend/src/LabelService.Contracts/Templates 一一对应。
 * 规范全文见 docs/contracts.md；改这里之前先读 AGENTS.md 的“契约变更”。
 * 长度单位一律是 mm，字号是 pt，颜色是 #RRGGBB。
 */

/**
 * 全部元素类型，按设计器元素库的顺序。
 */
export const ELEMENT_TYPES = ['text', 'barcode', 'qrcode', 'image', 'icon', 'line', 'rect'] as const

/**
 * 元素类型（JSON 里的 type 值）。
 */
export type ElementType = (typeof ELEMENT_TYPES)[number]

/**
 * 元素类型的中文名称，元素库、图层和属性面板里显示。
 */
export const ELEMENT_LABELS: Record<ElementType, string> = {
  text: '文本',
  barcode: '一维码',
  qrcode: '二维码',
  image: '图片',
  icon: '图标',
  line: '线',
  rect: '矩形',
}

/**
 * 字段值类型：字符串、数字、ISO 8601 日期（或日期时间）、PNG/JPG 的 base64。
 */
export type FieldType = 'string' | 'number' | 'date' | 'image'

/**
 * 水平对齐。
 */
export type TextAlign = 'left' | 'center' | 'right'

/**
 * 垂直对齐。
 */
export type VerticalAlign = 'top' | 'middle' | 'bottom'

/**
 * 文本放不下时的处理：单行裁切、在框宽内换行、单行缩小字号。
 */
export type TextFit = 'none' | 'wrap' | 'shrink'

/**
 * 一维码码制。
 */
export type BarcodeSymbology = 'code128' | 'code39' | 'ean13' | 'ean8' | 'upca' | 'itf14' | 'gs1-128'

/**
 * 二维码码制。
 */
export type TwoDimensionalSymbology = 'qr' | 'datamatrix' | 'pdf417'

/**
 * QR 纠错级别：约 7%、15%、25%、30%。
 */
export type ErrorCorrectionLevel = 'L' | 'M' | 'Q' | 'H'

/**
 * 图片和框比例不一致时的处理：完整放进框内、铺满裁掉多余、拉伸。
 */
export type ImageFit = 'contain' | 'cover' | 'stretch'

/**
 * 省略的文字样式取这些默认值，和服务端 TextStyle 的常量一致。
 */
export const TEXT_DEFAULTS = {
  fontFamily: 'SourceHanSansSC',
  fontSize: 10,
  color: '#000000',
  align: 'left' as TextAlign,
  vAlign: 'middle' as VerticalAlign,
  fit: 'none' as TextFit,
  lineHeight: 1.2,
}

/**
 * 默认线宽，mm。
 */
export const STROKE_DEFAULT_WIDTH = 0.3

/**
 * 标签纸张。选中画布预设后把宽高写进这里，之后改预设不影响已有模板。
 */
export interface LabelPage {
  /**
   * 宽度，mm，保留 1 位小数。
   */
  width: number
  /**
   * 高度，mm，保留 1 位小数。
   */
  height: number
  /**
   * 模板默认分辨率；请求里带 dpi 时以请求为准，都没有时用 300。
   */
  dpi?: number
}

/**
 * 模板需要的一项数据。元素通过 {{key}} 或 {{key:format}} 引用。
 */
export interface FieldDefinition {
  /**
   * 字段键：字母开头，只含字母、数字、下划线；模板内唯一，区分大小写。
   */
  key: string
  /**
   * 中文显示名，字段列表和演示程序的录入表格里显示。
   */
  name?: string
  /**
   * 值类型，决定校验规则和默认格式。
   */
  type: FieldType
  /**
   * 是否必填，默认 true；必填字段没传、没有自动值也没有默认值时返回 FIELD_REQUIRED。
   */
  required?: boolean
  /**
   * 数据来源：input 仅接口传入，template 仅模板提供；省略时保留旧版接口优先的回退规则。
   */
  source?: 'input' | 'template'
  /**
   * 调用时没传就用这个值，类型要和 type 匹配。
   */
  default?: unknown
  /**
   * 自动值：调用方没传时由服务端算出来，优先于 default（ADR 0012）。date 用 today、now、today+30d、today-1d、today+6m、today+1y；
   * number 用 index（第几条）、total（共几条）；string 用 client（调用方名称）、requestId（请求号）。
   */
  auto?: string
  /**
   * number、date 的默认显示格式（.NET 格式字符串）；绑定里写的格式优先。
   */
  format?: string
  /**
   * 样例值，设计器预览、JSON 样例和发布前试渲染用。
   */
  sample?: unknown
  /**
   * string 类型的最大长度（字符数）；不设则不限制。
   */
  maxLength?: number
}

/**
 * 文字样式。属性省略表示取默认值（TEXT_DEFAULTS），不必把默认值写进 JSON。
 */
export interface TextStyle {
  /**
   * 字体名，按服务端的字体匹配；找不到时降级到默认字体并给出 FONT_FALLBACK 警告。
   */
  fontFamily?: string
  /**
   * 字号，pt（1 pt ≈ 0.353 mm）。
   */
  fontSize?: number
  /**
   * 粗体。
   */
  bold?: boolean
  /**
   * 斜体。
   */
  italic?: boolean
  /**
   * 下划线。
   */
  underline?: boolean
  /**
   * 颜色 #RRGGBB。
   */
  color?: string
  /**
   * 水平对齐，默认左对齐。
   */
  align?: TextAlign
  /**
   * 垂直对齐，默认居中。
   */
  vAlign?: VerticalAlign
  /**
   * 放不下时的处理，默认 none（单行裁切）。
   */
  fit?: TextFit
  /**
   * fit 为 shrink 时最小缩到的字号，pt。
   */
  minFontSize?: number
  /**
   * 行高倍数：行高 = 字号 × lineHeight。
   */
  lineHeight?: number
  /**
   * 字间距，mm。
   */
  letterSpacing?: number
}

/**
 * 线型（线元素和矩形边框）。
 */
export interface StrokeStyle {
  /**
   * 线宽，mm，默认 0.3。
   */
  width?: number
  /**
   * 虚线的实段、空段长度（mm），如 [1, 0.5]；不设为实线。
   */
  dash?: number[]
  /**
   * 颜色 #RRGGBB，默认 #000000。
   */
  color?: string
}

/**
 * 全部元素共有的属性。坐标原点在标签左上角，x 向右、y 向下，单位 mm。
 */
interface ElementBase {
  /**
   * 元素 ID，模板内唯一，设计器生成。
   */
  id: string
  /**
   * 图层面板里显示的名字，可空。
   */
  name?: string
  /**
   * 左上角 x，mm。
   */
  x: number
  /**
   * 左上角 y，mm。
   */
  y: number
  /**
   * 宽度，mm；竖线为 0。
   */
  width: number
  /**
   * 高度，mm；横线为 0。
   */
  height: number
  /**
   * 旋转角度（度），顺时针，绕元素中心；条码只允许 0、90、180、270。
   */
  rotation?: number
  /**
   * 设计器里锁定，防止误拖；不影响渲染。
   */
  locked?: boolean
  /**
   * 隐藏的元素不渲染。
   */
  hidden?: boolean
}

/**
 * 文本元素。固定文字和字段可以混写，如“批次：{{batchNo}}”。
 */
export interface TextElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'text'
  /**
   * 文本内容，可含绑定。
   */
  content: string
  /**
   * 文字样式。
   */
  style?: TextStyle
}

/**
 * 一维码元素。模块宽度对齐打印点，见 docs/contracts.md 的“一维码尺寸规则”。
 */
export interface BarcodeElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'barcode'
  /**
   * 码制，默认 code128。
   */
  symbology?: BarcodeSymbology
  /**
   * 条码内容，可含绑定。
   */
  value: string
  /**
   * 条码比框窄时在框内的水平位置，默认左对齐。
   */
  align?: TextAlign
  /**
   * 是否显示人眼可读文字，默认显示。
   */
  showText?: boolean
  /**
   * 文字在条码下方还是上方，默认下方。
   */
  textPosition?: 'bottom' | 'top'
  /**
   * 人眼可读文字的样式。
   */
  textStyle?: TextStyle
  /**
   * 最小模块宽度（打印点），默认 2；按框宽算出的模块宽度小于它时返回 BARCODE_TOO_DENSE。
   */
  minModuleDots?: number
  /**
   * 框内左右留白，mm，默认 0（静区由框外留白保证）。
   */
  quietZone?: number
}

/**
 * 二维码元素，内容可以拼接多个字段，如 {{partNo}}|{{batchNo}}。
 */
export interface QrCodeElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'qrcode'
  /**
   * 码制，默认 qr。
   */
  symbology?: TwoDimensionalSymbology
  /**
   * 内容，可含绑定。
   */
  value: string
  /**
   * 纠错级别，只对 QR 有效，默认 M。
   */
  ecLevel?: ErrorCorrectionLevel
  /**
   * 框内四周留白，mm，默认 0。
   */
  quietZone?: number
}

/**
 * 图片元素。
 */
export interface ImageElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'image'
  /**
   * 来源：asset:素材key 引用素材库，或 {{字段}} 引用 image 类型的字段。
   */
  src: string
  /**
   * 比例不一致时的处理，默认 contain。
   */
  fit?: ImageFit
  /**
   * 转成黑白（抖动），热敏打印时建议开启。
   */
  mono?: boolean
}

/**
 * 图标元素，来自内置图标库（如 GB/T 191 包装储运图示标志）。
 */
export interface IconElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'icon'
  /**
   * 图标库路径，如 gb191/fragile。
   */
  icon: string
  /**
   * 颜色 #RRGGBB，默认 #000000。
   */
  color?: string
}

/**
 * 线元素：height 为 0 是横线，width 为 0 是竖线。
 */
export interface LineElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'line'
  /**
   * 线型。
   */
  stroke?: StrokeStyle
}

/**
 * 矩形元素，用来画外框和分区。
 */
export interface RectElement extends ElementBase {
  /**
   * 元素类型。
   */
  type: 'rect'
  /**
   * 边框；不设则没有边框。
   */
  stroke?: StrokeStyle
  /**
   * 填充色 #RRGGBB；不设则不填充。
   */
  fill?: string
  /**
   * 圆角半径，mm。
   */
  radius?: number
}

/**
 * 画布上的一个元素，按 type 区分具体类型。
 */
export type LabelElement =
  | TextElement
  | BarcodeElement
  | QrCodeElement
  | ImageElement
  | IconElement
  | LineElement
  | RectElement

/**
 * 一份完整的模板内容，对应一个模板版本。设计器、数据库和渲染引擎之间只靠它传递模板。
 */
export interface LabelTemplate {
  /**
   * 模板 JSON 规范版本。
   */
  schemaVersion: string
  /**
   * 模板编码，全局唯一，就是开放接口里的 templateCode；只含大写字母、数字和下划线。
   */
  code: string
  /**
   * 模板显示名，如“成品箱标”。
   */
  name: string
  /**
   * 标签尺寸和默认分辨率。
   */
  page: LabelPage
  /**
   * 调用方需要传的字段，随模板版本一起发布。
   */
  fields: FieldDefinition[]
  /**
   * 画布上的元素；数组顺序就是叠放顺序，后面的在上层。
   */
  elements: LabelElement[]
}

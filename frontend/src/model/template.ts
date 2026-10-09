/**
 * 模板 JSON 的 TypeScript 定义，和 backend/src/LabelService.Contracts/Templates 一一对应。
 * 规范全文见 docs/contracts.md；改这里之前先读 AGENTS.md 的“契约变更”。
 * 长度单位一律是 mm，字号是 pt，颜色是 #RRGGBB。
 */

export const ELEMENT_TYPES = ['text', 'barcode', 'qrcode', 'image', 'icon', 'line', 'rect'] as const
export type ElementType = (typeof ELEMENT_TYPES)[number]

export const ELEMENT_LABELS: Record<ElementType, string> = {
  text: '文本',
  barcode: '一维码',
  qrcode: '二维码',
  image: '图片',
  icon: '图标',
  line: '线',
  rect: '矩形',
}

export type FieldType = 'string' | 'number' | 'date' | 'image'
export type TextAlign = 'left' | 'center' | 'right'
export type VerticalAlign = 'top' | 'middle' | 'bottom'
export type TextFit = 'none' | 'wrap' | 'shrink'
export type BarcodeSymbology = 'code128' | 'code39' | 'ean13' | 'ean8' | 'upca' | 'itf14' | 'gs1-128'
export type TwoDimensionalSymbology = 'qr' | 'datamatrix' | 'pdf417'
export type ErrorCorrectionLevel = 'L' | 'M' | 'Q' | 'H'
export type ImageFit = 'contain' | 'cover' | 'stretch'

/** 省略的文字样式取这些默认值，和服务端 TextStyle 的常量一致。 */
export const TEXT_DEFAULTS = {
  fontFamily: 'SourceHanSansSC',
  fontSize: 10,
  color: '#000000',
  align: 'left' as TextAlign,
  vAlign: 'middle' as VerticalAlign,
  fit: 'none' as TextFit,
  lineHeight: 1.2,
}

export const STROKE_DEFAULT_WIDTH = 0.3

export interface LabelPage {
  width: number
  height: number
  dpi?: number
}

export interface FieldDefinition {
  key: string
  name?: string
  type: FieldType
  required?: boolean
  default?: unknown
  format?: string
  sample?: unknown
  maxLength?: number
}

export interface TextStyle {
  fontFamily?: string
  fontSize?: number
  bold?: boolean
  italic?: boolean
  underline?: boolean
  color?: string
  align?: TextAlign
  vAlign?: VerticalAlign
  fit?: TextFit
  minFontSize?: number
  lineHeight?: number
  letterSpacing?: number
}

export interface StrokeStyle {
  width?: number
  dash?: number[]
  color?: string
}

interface ElementBase {
  id: string
  name?: string
  x: number
  y: number
  width: number
  height: number
  rotation?: number
  locked?: boolean
  hidden?: boolean
}

export interface TextElement extends ElementBase {
  type: 'text'
  content: string
  style?: TextStyle
}

export interface BarcodeElement extends ElementBase {
  type: 'barcode'
  symbology?: BarcodeSymbology
  value: string
  align?: TextAlign
  showText?: boolean
  textPosition?: 'bottom' | 'top'
  textStyle?: TextStyle
  minModuleDots?: number
  quietZone?: number
}

export interface QrCodeElement extends ElementBase {
  type: 'qrcode'
  symbology?: TwoDimensionalSymbology
  value: string
  ecLevel?: ErrorCorrectionLevel
  quietZone?: number
}

export interface ImageElement extends ElementBase {
  type: 'image'
  src: string
  fit?: ImageFit
  mono?: boolean
}

export interface IconElement extends ElementBase {
  type: 'icon'
  icon: string
  color?: string
}

export interface LineElement extends ElementBase {
  type: 'line'
  stroke?: StrokeStyle
}

export interface RectElement extends ElementBase {
  type: 'rect'
  stroke?: StrokeStyle
  fill?: string
  radius?: number
}

export type LabelElement =
  | TextElement
  | BarcodeElement
  | QrCodeElement
  | ImageElement
  | IconElement
  | LineElement
  | RectElement

export interface LabelTemplate {
  schemaVersion: string
  code: string
  name: string
  page: LabelPage
  fields: FieldDefinition[]
  elements: LabelElement[]
}

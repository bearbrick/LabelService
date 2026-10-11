/**
 * 在 Konva 组里画出一个元素。组的坐标系单位是 mm，原点在元素左上角；旋转由组处理（绕中心、顺时针）。
 * 规则尽量和服务端绘制器一致：矩形边框画在框内侧、线宽以线为中心、一维码模块宽度取整到打印点。
 * 文字在 drawText.ts，一维码、二维码在 drawCodes.ts，这里是入口和图片、图标、线、矩形。
 */
import Konva from 'konva'
import type { DisplayMode } from '../model/bindings.ts'
import { findIcon } from '../model/icons.ts'
import { STROKE_DEFAULT_WIDTH } from '../model/template.ts'
import type { FieldDefinition, IconElement, ImageElement, LabelElement, LineElement, RectElement } from '../model/template.ts'
import { drawBarcode, drawMatrix } from './drawCodes.ts'
import { drawText, textNode } from './drawText.ts'
import { getImage, imageSize } from './images.ts'

/**
 * 给属性面板显示的提示。
 */
export interface Metric {
  /**
   * 提示文字；空字符串表示没有提示。
   */
  text: string
  /**
   * 语气：good 绿色（正常的度量信息），bad 红色（会影响打印的问题），空为普通。
   */
  tone: '' | 'good' | 'bad'
}

/**
 * 绘制元素需要的上下文（由画布提供）。
 */
export interface DrawContext {
  /**
   * 显示样例值还是占位符。
   */
  mode: DisplayMode
  /**
   * 模板的字段（取样例值）。
   */
  fields: readonly FieldDefinition[]
  /**
   * 模板的分辨率（条码模块按它取整到打印点）。
   */
  dpi: number
  /**
   * 素材 ID 对应的图片地址；找不到返回 null。
   * @param id 素材 key。
   * @returns 图片地址，或 null。
   */
  assetSource(id: string): string | null
  /**
   * 图片等异步资源加载完成后调用，让画布重画。
   */
  invalidate(): void
  /**
   * 记录元素的提示（如条码过密、文字超框）。
   * @param id 元素 ID。
   * @param metric 提示。
   */
  report(id: string, metric: Metric): void
}

/**
 * 图片占位符的图标路径（24×24 网格）。
 */
const PLACEHOLDER_IMAGE = 'M4 5h16v14H4z M4 16l5-5 4 4 2-2 5 5 M14 9.5a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0-3 0'

/**
 * 组的位置：以元素中心为旋转原点。
 * @param group 元素的组。
 * @param element 元素。
 */
export function setGeometry(group: Konva.Group, element: LabelElement): void {
  group.setAttrs({
    x: element.x + element.width / 2,
    y: element.y + element.height / 2,
    offsetX: element.width / 2,
    offsetY: element.height / 2,
    rotation: element.rotation ?? 0,
  })
}

/**
 * 清空组并重画元素。
 * @param group 元素的组。
 * @param element 元素。
 * @param context 绘制上下文。
 */
export function drawElement(group: Konva.Group, element: LabelElement, context: DrawContext): void {
  group.destroyChildren()
  group.add(new Konva.Rect({ name: 'hit', fill: 'transparent', ...hitArea(element) }))
  switch (element.type) {
    case 'text':
      return drawText(group, element, context)
    case 'barcode':
      return drawBarcode(group, element, context)
    case 'qrcode':
      return drawMatrix(group, element, context)
    case 'image':
      return drawImage(group, element, context)
    case 'icon':
      return drawIcon(group, element)
    case 'line':
      return drawLine(group, element)
    case 'rect':
      return drawRect(group, element)
  }
}

/**
 * 点击区域：线条放宽到 2.4mm；没有填充的矩形只有边框一圈可以点中（常见的标签外框不会挡住里面的框选）；其他元素整个框都能点中。
 * @param element 元素。
 * @returns 点击区域矩形的属性。
 */
function hitArea(element: LabelElement): Konva.RectConfig {
  if (element.type === 'line' && element.height === 0) return { x: 0, y: -1.2, width: element.width, height: 2.4 }
  if (element.type === 'line' && element.width === 0) return { x: -1.2, y: 0, width: 2.4, height: element.height }
  if (element.type === 'rect' && !element.fill) {
    return { x: 0, y: 0, width: element.width, height: element.height, fill: undefined, fillEnabled: false, stroke: 'transparent', strokeWidth: 0.01, hitStrokeWidth: 1.6 }
  }

  return { x: 0, y: 0, width: Math.max(element.width, 0.5), height: Math.max(element.height, 0.5) }
}

/**
 * 画图片占位符：灰色虚线框、图片图标和说明（如字段名、素材不存在）。
 * @param group 元素的组。
 * @param element 元素。
 * @param label 说明；空字符串表示不写（图片正在加载）。
 */
function drawPlaceholder(group: Konva.Group, element: LabelElement, label: string): void {
  const { width, height } = element
  group.add(new Konva.Rect({ width, height, fill: 'rgba(120,130,140,0.08)', stroke: '#8A939D', strokeWidth: 0.2, dash: [0.8, 0.6], listening: false }))
  const size = Math.min(width, height) * 0.45
  group.add(new Konva.Path({
    data: PLACEHOLDER_IMAGE,
    x: (width - size) / 2,
    y: (height - size) / 2 - (label ? size * 0.18 : 0),
    scaleX: size / 24,
    scaleY: size / 24,
    stroke: '#8A939D',
    strokeWidth: 1.5,
    lineCap: 'round',
    lineJoin: 'round',
    listening: false,
  }))
  if (label && height > 6) {
    group.add(textNode({ y: height / 2 + size * 0.38, width, text: label, align: 'center', fontSize: Math.min(2.2, height / 6), fill: '#6B747E', listening: false }))
  }
}

/**
 * 画图片元素：素材按 key 取地址，字段图片用样例值；按 fit 缩放或裁切；还没加载好时先画占位符。
 * @param group 元素的组。
 * @param element 图片元素。
 * @param context 绘制上下文。
 */
function drawImage(group: Konva.Group, element: ImageElement, context: DrawContext): void {
  const src = element.src ?? ''
  let url: string | null = null
  let label = ''
  if (src.startsWith('asset:')) {
    url = context.assetSource(src.slice(6))
    label = url ? '' : '素材不存在'
  } else {
    const key = /\{\{\s*([A-Za-z][A-Za-z0-9_]*)/.exec(src)?.[1]
    label = key ? `{{${key}}}` : '未设置来源'
    const sample = key ? context.fields.find((field) => field.key === key)?.sample : undefined
    if (typeof sample === 'string' && sample && context.mode === 'sample') {
      url = sample.startsWith('data:') ? sample : `data:image/png;base64,${sample}`
    }
  }

  if (!url) return drawPlaceholder(group, element, label)
  const image = getImage(url, element.mono ?? false, context.invalidate)
  if (!image) return drawPlaceholder(group, element, '')
  const natural = imageSize(image)
  const { width, height } = element
  const attrs: Konva.ImageConfig = { image, listening: false }
  if (element.fit === 'stretch') {
    Object.assign(attrs, { x: 0, y: 0, width, height })
  } else if (element.fit === 'cover') {
    const scale = Math.max(width / natural.width, height / natural.height)
    const cropWidth = width / scale
    const cropHeight = height / scale
    Object.assign(attrs, { x: 0, y: 0, width, height, crop: { x: (natural.width - cropWidth) / 2, y: (natural.height - cropHeight) / 2, width: cropWidth, height: cropHeight } })
  } else {
    const scale = Math.min(width / natural.width, height / natural.height)
    Object.assign(attrs, { x: (width - natural.width * scale) / 2, y: (height - natural.height * scale) / 2, width: natural.width * scale, height: natural.height * scale })
  }

  group.add(new Konva.Image(attrs))
}

/**
 * 画图标元素：图标路径拉伸到元素框。
 * @param group 元素的组。
 * @param element 图标元素。
 */
function drawIcon(group: Konva.Group, element: IconElement): void {
  group.add(new Konva.Path({
    data: findIcon(element.icon).d,
    scaleX: element.width / 24,
    scaleY: element.height / 24,
    stroke: element.color ?? '#000000',
    strokeWidth: 1.7,
    lineCap: 'round',
    lineJoin: 'round',
    listening: false,
  }))
}

/**
 * 画线元素：横线、竖线或斜线，线宽以线为中心。
 * @param group 元素的组。
 * @param element 线元素。
 */
function drawLine(group: Konva.Group, element: LineElement): void {
  const stroke = element.stroke ?? {}
  const width = stroke.width ?? STROKE_DEFAULT_WIDTH
  if (width <= 0) return
  const points = element.height === 0 ? [0, 0, element.width, 0] : element.width === 0 ? [0, 0, 0, element.height] : [0, 0, element.width, element.height]
  group.add(new Konva.Line({ points, stroke: stroke.color ?? '#000000', strokeWidth: width, dash: stroke.dash, listening: false }))
}

/**
 * 边框画在框内侧，外沿和元素框对齐（和服务端 RectPainter 一致）。
 * @param group 元素的组。
 * @param element 矩形元素。
 */
function drawRect(group: Konva.Group, element: RectElement): void {
  const { width, height } = element
  const radius = element.radius ?? 0
  if (element.fill) group.add(new Konva.Rect({ width, height, cornerRadius: radius, fill: element.fill, listening: false }))
  const stroke = element.stroke
  const strokeWidth = stroke?.width ?? 0
  if (!stroke || strokeWidth <= 0) return
  group.add(new Konva.Rect({
    x: strokeWidth / 2,
    y: strokeWidth / 2,
    width: Math.max(0, width - strokeWidth),
    height: Math.max(0, height - strokeWidth),
    cornerRadius: Math.max(0, radius - strokeWidth / 2),
    stroke: stroke.color ?? '#000000',
    strokeWidth,
    dash: stroke.dash,
    listening: false,
  }))
}

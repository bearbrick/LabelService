/**
 * 在 Konva 组里画出一个元素。组的坐标系单位是 mm，原点在元素左上角；旋转由组处理（绕中心、顺时针）。
 * 规则尽量和服务端绘制器一致：矩形边框画在框内侧、线宽以线为中心、一维码模块宽度取整到打印点。
 */
import Konva from 'konva'
import { resolveText } from '../model/bindings.ts'
import type { DisplayMode } from '../model/bindings.ts'
import { cssFontFamily } from '../model/fonts.ts'
import { findIcon } from '../model/icons.ts'
import { STROKE_DEFAULT_WIDTH, TEXT_DEFAULTS } from '../model/template.ts'
import type { BarcodeElement, FieldDefinition, IconElement, ImageElement, LabelElement, LineElement, QrCodeElement, RectElement, TextElement } from '../model/template.ts'
import { MM_PER_PT } from '../model/units.ts'
import { encode, moduleDots, SYMBOLOGY_LABELS } from './barcode.ts'
import { getImage, imageSize } from './images.ts'

/** 给属性面板显示的提示。 */
export interface Metric {
  text: string
  tone: '' | 'good' | 'bad'
}

export interface DrawContext {
  mode: DisplayMode
  fields: readonly FieldDefinition[]
  dpi: number
  /** 素材 ID 对应的图片地址；找不到返回 null。 */
  assetSource(id: string): string | null
  /** 图片等异步资源加载完成后调用，让画布重画。 */
  invalidate(): void
  /** 记录元素的提示（如条码过密、文字超框）。 */
  report(id: string, metric: Metric): void
}

const PLACEHOLDER_IMAGE = 'M4 5h16v14H4z M4 16l5-5 4 4 2-2 5 5 M14 9.5a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0-3 0'
const PROBLEM = '#C03A28'
const measurer = new Konva.Text({ text: '' })
/** Konva 的宽高支持 'auto'（按内容测量），但类型声明只写了 number。 */
const AUTO = 'auto' as unknown as number

/**
 * 文字按 TEXT_SCALE 倍排版，节点再缩小回来。画布单位是 mm，字号只有几个“像素”，而浏览器的字体度量
 * （Konva 用 fontBoundingBoxAscent / Descent 定基线）按整像素取整，误差能到 0.3 mm；放大后可以忽略，和服务端位置一致（T-016）。
 */
const TEXT_SCALE = 100

/** 把以 mm 为单位的文字属性放大 TEXT_SCALE 倍（位置不变，靠节点缩放还原）。 */
function scaled(attrs: Konva.TextConfig): Konva.TextConfig {
  const up = (value: number | undefined) => (typeof value === 'number' ? value * TEXT_SCALE : value)
  return { ...attrs, fontSize: up(attrs.fontSize ?? 12), letterSpacing: up(attrs.letterSpacing), padding: up(attrs.padding), width: up(attrs.width), height: up(attrs.height) }
}

function textNode(attrs: Konva.TextConfig): Konva.Text {
  return new Konva.Text({ ...scaled(attrs), scaleX: 1 / TEXT_SCALE, scaleY: 1 / TEXT_SCALE })
}

function measure(attrs: Konva.TextConfig): { width: number; height: number } {
  measurer.setAttrs(scaled({ width: AUTO, height: AUTO, wrap: 'none', padding: 0, ...attrs }))
  return { width: measurer.width() / TEXT_SCALE, height: measurer.height() / TEXT_SCALE }
}

/** 组的位置：以元素中心为旋转原点。 */
export function setGeometry(group: Konva.Group, element: LabelElement): void {
  group.setAttrs({
    x: element.x + element.width / 2,
    y: element.y + element.height / 2,
    offsetX: element.width / 2,
    offsetY: element.height / 2,
    rotation: element.rotation ?? 0,
  })
}

/** 清空组并重画元素。 */
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
 */
function hitArea(element: LabelElement): Konva.RectConfig {
  if (element.type === 'line' && element.height === 0) return { x: 0, y: -1.2, width: element.width, height: 2.4 }
  if (element.type === 'line' && element.width === 0) return { x: -1.2, y: 0, width: 2.4, height: element.height }
  if (element.type === 'rect' && !element.fill) {
    return { x: 0, y: 0, width: element.width, height: element.height, fill: undefined, fillEnabled: false, stroke: 'transparent', strokeWidth: 0.01, hitStrokeWidth: 1.6 }
  }

  return { x: 0, y: 0, width: Math.max(element.width, 0.5), height: Math.max(element.height, 0.5) }
}

function drawProblem(group: Konva.Group, element: LabelElement, message: string): void {
  const width = Math.max(element.width, 4)
  const height = Math.max(element.height, 4)
  group.add(new Konva.Rect({ width, height, fill: 'rgba(192,58,40,0.07)', stroke: PROBLEM, strokeWidth: 0.25, dash: [1, 0.6], listening: false }))
  group.add(textNode({
    x: 0.8,
    y: 0.8,
    width: width - 1.6,
    height: height - 1.6,
    text: message,
    fontSize: Math.min(2.6, Math.max(1.4, height / 4)),
    fontFamily: cssFontFamily(undefined),
    fill: PROBLEM,
    wrap: 'char',
    align: 'center',
    verticalAlign: 'middle',
    listening: false,
  }))
}

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

function drawText(group: Konva.Group, element: TextElement, context: DrawContext): void {
  const style = { ...TEXT_DEFAULTS, ...element.style }
  const { width, height } = element
  const text = resolveText(element.content, context.fields, context.mode)
  const maxSize = style.fontSize * MM_PER_PT
  const base: Konva.TextConfig = {
    text,
    fontFamily: cssFontFamily(style.fontFamily),
    fontStyle: style.bold && style.italic ? 'italic bold' : style.bold ? 'bold' : style.italic ? 'italic' : 'normal',
    letterSpacing: style.letterSpacing ?? 0,
    lineHeight: style.lineHeight,
  }
  let fontSize = maxSize
  if (style.fit === 'shrink' && text) {
    const fits = (size: number) => {
      const box = measure({ ...base, fontSize: size })
      return box.width <= width + 1e-3 && box.height <= height + 1e-3
    }

    if (!fits(maxSize)) {
      let low = Math.max(1, (style.minFontSize ?? 4) * MM_PER_PT)
      let high = maxSize
      if (fits(low)) {
        for (let i = 0; i < 14; i++) {
          const middle = (low + high) / 2
          if (fits(middle)) low = middle
          else high = middle
        }
      }

      fontSize = low
    }
  }

  const clip = new Konva.Group({ clip: { x: 0, y: 0, width, height }, listening: false })
  clip.add(textNode({
    ...base,
    width,
    height,
    fontSize,
    align: style.align,
    verticalAlign: style.vAlign,
    wrap: style.fit === 'wrap' ? 'word' : 'none',
    ellipsis: false,
    fill: style.color,
    textDecoration: style.underline ? 'underline' : '',
    listening: false,
  }))
  group.add(clip)

  const used = measure({ ...base, fontSize, width: style.fit === 'wrap' ? width : AUTO, wrap: style.fit === 'wrap' ? 'word' : 'none' })
  const overflow = (style.fit !== 'wrap' && used.width > width + 0.05) || used.height > height + 0.05
  if (fontSize < maxSize - 1e-3) {
    context.report(element.id, overflow
      ? { text: `缩到最小字号 ${style.minFontSize ?? 4} pt 仍放不下，超出部分不会打印`, tone: 'bad' }
      : { text: `已自动缩小到 ${(fontSize / MM_PER_PT).toFixed(1)} pt`, tone: '' })
  } else if (overflow) {
    context.report(element.id, { text: '文字超出文本框，超出部分不会打印。可以改成自动换行或自动缩小。', tone: 'bad' })
  } else {
    context.report(element.id, { text: '', tone: '' })
  }
}

/** 条码内容总是按样例值编码；占位符模式下文字仍显示 {{key}}。 */
function barcodeContent(value: string, context: DrawContext): { content: string; caption: string; problem: string | null } {
  const content = resolveText(value, context.fields, 'sample')
  const caption = context.mode === 'placeholder' ? value : content
  if (!content) return { content, caption, problem: '条码内容为空' }
  if (content.includes('{{')) return { content, caption, problem: '字段没有样例值，无法预览条码' }
  return { content, caption, problem: null }
}

function drawBarcode(group: Konva.Group, element: BarcodeElement, context: DrawContext): void {
  const symbology = element.symbology ?? 'code128'
  const label = SYMBOLOGY_LABELS[symbology] ?? symbology
  const { content, caption, problem } = barcodeContent(element.value, context)
  if (problem) {
    drawProblem(group, element, problem)
    context.report(element.id, { text: problem, tone: 'bad' })
    return
  }

  const encoded = encode(symbology, content)
  if (!encoded.ok || encoded.code.kind !== 'linear') {
    const message = encoded.ok ? '编码结果不是一维码' : encoded.message
    drawProblem(group, element, `内容不符合 ${label}：${message}`)
    context.report(element.id, { text: `内容“${content}”不符合 ${label} 规则：${message}`, tone: 'bad' })
    return
  }

  const { widths, modules } = encoded.code
  const dotsPerMm = context.dpi / 25.4
  const quiet = Math.max(0, element.quietZone ?? 0)
  const usable = Math.max(0, element.width - quiet * 2)
  const showText = element.showText ?? true
  const textSize = showText ? (element.textStyle?.fontSize ?? 8) * MM_PER_PT : 0
  const textHeight = showText ? textSize * 1.2 : 0
  const barHeight = Math.max(1, element.height - textHeight)
  const minDots = element.minModuleDots ?? 2
  const dots = moduleDots(usable, modules, context.dpi)
  const module = Math.max(1, dots) / dotsPerMm
  const barsWidth = modules * module
  const align = element.align ?? 'left'
  const x0 = quiet + (align === 'center' ? (usable - barsWidth) / 2 : align === 'right' ? usable - barsWidth : 0)
  const barY = showText && element.textPosition === 'top' ? textHeight : 0
  group.add(new Konva.Shape({
    fill: '#000000',
    listening: false,
    sceneFunc(canvas, shape) {
      canvas.beginPath()
      let x = x0
      for (let i = 0; i < widths.length; i++) {
        const width = widths[i] * module
        if (i % 2 === 0) canvas.rect(x, barY, width, barHeight)
        x += width
      }

      canvas.fillShape(shape)
    },
  }))

  if (showText) {
    const fontFamily = cssFontFamily(element.textStyle?.fontFamily)
    const textWidth = Math.max(barsWidth, Math.min(element.width, measure({ text: caption, fontSize: textSize, fontFamily }).width))
    group.add(textNode({
      x: barsWidth >= textWidth ? x0 : Math.max(0, x0 + (barsWidth - textWidth) / 2),
      y: element.textPosition === 'top' ? 0 : barHeight + textSize * 0.12,
      width: textWidth,
      text: caption,
      fontSize: textSize,
      fontFamily,
      align: 'center',
      fill: '#000000',
      wrap: 'none',
      listening: false,
    }))
  }

  if (dots < minDots) {
    const need = Math.ceil(((modules * minDots) / dotsPerMm + quiet * 2) * 10) / 10
    context.report(element.id, {
      text: `条码过密：${modules} 个模块，框宽只够每个模块 ${dots} 点（要求至少 ${minDots} 点 @${context.dpi}dpi）。把宽度加到 ${need} mm，或缩短内容。`,
      tone: 'bad',
    })
  } else {
    context.report(element.id, {
      text: `${modules} 个模块 · 每模块 ${dots} 点（${module.toFixed(3)} mm @${context.dpi}dpi）· 实际条宽 ${barsWidth.toFixed(1)} mm`,
      tone: 'good',
    })
  }
}

function drawMatrix(group: Konva.Group, element: QrCodeElement, context: DrawContext): void {
  const symbology = element.symbology ?? 'qr'
  const label = SYMBOLOGY_LABELS[symbology] ?? symbology
  const content = resolveText(element.value, context.fields, 'sample')
  if (!content || content.includes('{{')) {
    const message = content ? '字段没有样例值，无法预览二维码' : '二维码内容为空'
    drawProblem(group, element, message)
    context.report(element.id, { text: message, tone: 'bad' })
    return
  }

  const encoded = encode(symbology, content, element.ecLevel ?? 'M')
  if (!encoded.ok || encoded.code.kind !== 'matrix') {
    const message = encoded.ok ? '编码结果不是二维码' : encoded.message
    drawProblem(group, element, `${label}：${message}`)
    context.report(element.id, { text: `${label} 编码失败：${message}`, tone: 'bad' })
    return
  }

  const { pixels, columns, rows } = encoded.code
  const dotsPerMm = context.dpi / 25.4
  const quiet = Math.max(0, element.quietZone ?? 0)
  const width = Math.max(0, element.width - quiet * 2)
  const height = Math.max(0, element.height - quiet * 2)
  const dots = Math.floor(Math.min((width * dotsPerMm) / columns, (height * dotsPerMm) / rows) + 1e-6)
  const module = Math.max(1, dots) / dotsPerMm
  const codeWidth = columns * module
  const codeHeight = rows * module
  const x0 = (element.width - codeWidth) / 2
  const y0 = (element.height - codeHeight) / 2
  group.add(new Konva.Shape({
    fill: '#000000',
    listening: false,
    sceneFunc(canvas, shape) {
      canvas.beginPath()
      for (let y = 0; y < rows; y++) {
        let x = 0
        while (x < columns) {
          if (pixels[y * columns + x]) {
            const start = x
            while (x < columns && pixels[y * columns + x]) x++
            canvas.rect(x0 + start * module, y0 + y * module, (x - start) * module, module)
          } else {
            x++
          }
        }
      }

      canvas.fillShape(shape)
    },
  }))

  context.report(element.id, dots < 2
    ? { text: `码太密：${columns}×${rows} 个模块，每模块不到 2 点。把尺寸加大，或减少内容。`, tone: 'bad' }
    : { text: `${columns}×${rows} 个模块 · 每模块 ${dots} 点（${module.toFixed(3)} mm）· 实际 ${codeWidth.toFixed(1)}×${codeHeight.toFixed(1)} mm`, tone: 'good' })
}

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

function drawLine(group: Konva.Group, element: LineElement): void {
  const stroke = element.stroke ?? {}
  const width = stroke.width ?? STROKE_DEFAULT_WIDTH
  if (width <= 0) return
  const points = element.height === 0 ? [0, 0, element.width, 0] : element.width === 0 ? [0, 0, 0, element.height] : [0, 0, element.width, element.height]
  group.add(new Konva.Line({ points, stroke: stroke.color ?? '#000000', strokeWidth: width, dash: stroke.dash, listening: false }))
}

/** 边框画在框内侧，外沿和元素框对齐（和服务端 RectPainter 一致）。 */
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

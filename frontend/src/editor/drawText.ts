/**
 * 文字的绘制和测量：文本元素（对齐、换行、自动缩小、超框提示），以及条码文字、错误提示框用的文字节点。
 * 画布单位是 mm，文字按 TEXT_SCALE 倍排版再缩小回来，和服务端的位置一致。
 */
import Konva from 'konva'
import { resolveText } from '../model/bindings.ts'
import { cssFontFamily } from '../model/fonts.ts'
import { TEXT_DEFAULTS } from '../model/template.ts'
import type { LabelElement, TextElement } from '../model/template.ts'
import { MM_PER_PT } from '../model/units.ts'
import type { DrawContext } from './draw.ts'

/**
 * 问题提示的颜色（红）。
 */
const PROBLEM = '#C03A28'

/**
 * 测量文字尺寸用的文字节点（不加到画布上，反复改属性测量）。
 */
const measurer = new Konva.Text({ text: '' })

/**
 * Konva 的宽高支持 'auto'（按内容测量），但类型声明只写了 number。
 */
const AUTO = 'auto' as unknown as number

/**
 * 文字按 TEXT_SCALE 倍排版，节点再缩小回来。画布单位是 mm，字号只有几个“像素”，而浏览器的字体度量
 * （Konva 用 fontBoundingBoxAscent / Descent 定基线）按整像素取整，误差能到 0.3 mm；放大后可以忽略，和服务端位置一致。
 */
const TEXT_SCALE = 100

/**
 * 把以 mm 为单位的文字属性放大 TEXT_SCALE 倍（位置不变，靠节点缩放还原）。
 * @param attrs 文字属性（mm）。
 * @returns 放大后的属性。
 */
function scaled(attrs: Konva.TextConfig): Konva.TextConfig {
  const up = (value: number | undefined) => (typeof value === 'number' ? value * TEXT_SCALE : value)
  return { ...attrs, fontSize: up(attrs.fontSize ?? 12), letterSpacing: up(attrs.letterSpacing), padding: up(attrs.padding), width: up(attrs.width), height: up(attrs.height) }
}

/**
 * 建一个文字节点：属性按 mm 写，内部放大排版再缩小回来。
 * @param attrs 文字属性（mm）。
 * @returns 文字节点。
 */
export function textNode(attrs: Konva.TextConfig): Konva.Text {
  return new Konva.Text({ ...scaled(attrs), scaleX: 1 / TEXT_SCALE, scaleY: 1 / TEXT_SCALE })
}

/**
 * 测量文字在不换行、无内边距时的尺寸（可以覆盖成按宽度换行）。
 * @param attrs 文字属性（mm）。
 * @returns 宽高，mm。
 */
export function measure(attrs: Konva.TextConfig): { width: number; height: number } {
  measurer.setAttrs(scaled({ width: AUTO, height: AUTO, wrap: 'none', padding: 0, ...attrs }))
  return { width: measurer.width() / TEXT_SCALE, height: measurer.height() / TEXT_SCALE }
}

/**
 * 画问题提示：红色虚线框加居中的说明文字（如条码内容不合规）。
 * @param group 元素的组。
 * @param element 元素（框至少 4×4 mm）。
 * @param message 说明。
 */
export function drawProblem(group: Konva.Group, element: LabelElement, message: string): void {
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

/**
 * 画文本元素：绑定替换、字体样式、对齐；自动缩小时二分查找能放下的字号；超出框的部分裁掉并提示。
 * @param group 元素的组。
 * @param element 文本元素。
 * @param context 绘制上下文。
 */
export function drawText(group: Konva.Group, element: TextElement, context: DrawContext): void {
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

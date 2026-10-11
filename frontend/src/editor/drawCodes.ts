/**
 * 一维码和二维码的绘制：按样例值编码（bwip-js），模块宽度取整到打印点（和服务端一致），过密、内容不合规时提示。
 */
import Konva from 'konva'
import { resolveText } from '../model/bindings.ts'
import { cssFontFamily } from '../model/fonts.ts'
import type { BarcodeElement, QrCodeElement } from '../model/template.ts'
import { MM_PER_PT } from '../model/units.ts'
import { encode, moduleDots, SYMBOLOGY_LABELS } from './barcode.ts'
import type { DrawContext } from './draw.ts'
import { drawProblem, measure, textNode } from './drawText.ts'

/**
 * 条码内容总是按样例值编码；占位符模式下文字仍显示 {{key}}。
 * @param value 条码内容（可含绑定）。
 * @param context 绘制上下文。
 * @returns 编码用的内容、显示的文字，以及不能编码的原因（没有问题时为 null）。
 */
function barcodeContent(value: string, context: DrawContext): { content: string; caption: string; problem: string | null } {
  const content = resolveText(value, context.fields, 'sample')
  const caption = context.mode === 'placeholder' ? value : content
  if (!content) return { content, caption, problem: '条码内容为空' }
  if (content.includes('{{')) return { content, caption, problem: '字段没有样例值，无法预览条码' }
  return { content, caption, problem: null }
}

/**
 * 画一维码：模块宽度按框宽取整到打印点，按 align 放在框里，人眼可读文字在上方或下方；报告模块数和是否过密。
 * @param group 元素的组。
 * @param element 一维码元素。
 * @param context 绘制上下文。
 */
export function drawBarcode(group: Konva.Group, element: BarcodeElement, context: DrawContext): void {
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

  // 模块宽度按框宽（扣掉左右留白）取整到打印点，和服务端一样；再按 align 决定条码在框里的起点。
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
  // 文字在上方时条码往下让出一行文字的高度。
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
    // 人眼可读文字以条码为中心，比条码宽时向两边伸出（不超出元素框）。
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

  // 报告给属性面板：过密时给出需要的最小宽度，否则给出实际的模块尺寸。
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

/**
 * 画二维码：模块按框的短边取整到打印点，在框里居中；同一行连续的黑模块合并成一个矩形画。
 * @param group 元素的组。
 * @param element 二维码元素。
 * @param context 绘制上下文。
 */
export function drawMatrix(group: Konva.Group, element: QrCodeElement, context: DrawContext): void {
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

  // 二维码的模块按框的短边取整到打印点，整体在框里居中。
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

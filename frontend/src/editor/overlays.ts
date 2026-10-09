/**
 * 画布的辅助功能：变换框能用哪些手柄、网格间距和网格线、按住拖动平移。不依赖 Konva。
 */
import type { LabelElement, LabelPage } from '../model/template.ts'

export const ALL_ANCHORS = ['top-left', 'top-center', 'top-right', 'middle-right', 'middle-left', 'bottom-left', 'bottom-center', 'bottom-right']

export interface TransformRules {
  anchors: string[]
  rotate: boolean
  keepRatio: boolean
  /** 横线只能拉宽度，竖线只能拉高度。 */
  mode: 'box' | 'hline' | 'vline'
  locked: boolean
}

/** 按选中的元素决定变换框：线只能拉长度、不能旋转；二维码和图标等比缩放；有锁定的元素时不能变换。 */
export function transformRules(selected: readonly LabelElement[]): TransformRules {
  const rules: TransformRules = { anchors: ALL_ANCHORS, rotate: true, keepRatio: false, mode: 'box', locked: selected.some((element) => element.locked) }
  if (selected.length !== 1) return rules
  const element = selected[0]
  if (element.type === 'line') {
    rules.rotate = false
    if (element.height === 0) Object.assign(rules, { anchors: ['middle-left', 'middle-right'], mode: 'hline' })
    else if (element.width === 0) Object.assign(rules, { anchors: ['top-center', 'bottom-center'], mode: 'vline' })
  }

  if (element.type === 'qrcode' || element.type === 'icon') Object.assign(rules, { anchors: ['top-left', 'top-right', 'bottom-left', 'bottom-right'], keepRatio: true })
  return rules
}

const GRID_STEPS = [0.5, 1, 2, 5, 10, 20, 50, 100]

/** 实际画出来的网格间距（mm）：从设置的间距开始，缩小时间距不足 6 像素就放大一档。 */
export function gridStep(gridSize: number, pxPerMm: number): number {
  let index = Math.max(0, GRID_STEPS.indexOf(gridSize))
  while (index < GRID_STEPS.length - 1 && GRID_STEPS[index] * pxPerMm < 6) index++
  return GRID_STEPS[index]
}

/** 在纸张坐标（mm）里画网格线，线宽 1 像素。 */
export function drawGrid(canvas: CanvasRenderingContext2D, page: LabelPage, gridSize: number, pxPerMm: number): void {
  const step = gridStep(gridSize, pxPerMm)
  canvas.save()
  canvas.lineWidth = 1 / pxPerMm
  canvas.strokeStyle = 'rgba(21,85,212,0.07)'
  canvas.beginPath()
  for (let x = step; x < page.width - 1e-6; x += step) {
    canvas.moveTo(x, 0)
    canvas.lineTo(x, page.height)
  }

  for (let y = step; y < page.height - 1e-6; y += step) {
    canvas.moveTo(0, y)
    canvas.lineTo(page.width, y)
  }

  canvas.stroke()
  canvas.restore()
}

/** 按住拖动平移：从 origin 出发，指针每次移动回调新的位置，松开时调用 end。 */
export function dragPan(event: PointerEvent, origin: { x: number; y: number }, move: (x: number, y: number) => void, end: () => void): void {
  const startX = event.clientX
  const startY = event.clientY
  const onMove = (moveEvent: PointerEvent) => move(origin.x + moveEvent.clientX - startX, origin.y + moveEvent.clientY - startY)
  const onUp = () => {
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    end()
  }

  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

/**
 * 画布上方和左侧的毫米标尺：标出纸张范围、选中元素的范围和光标位置。
 */
/**
 * 画标尺需要的视图状态。
 */
export interface RulerView {
  /**
   * 纸张原点在画布上的屏幕 x（px）。
   */
  originX: number
  /**
   * 纸张原点在画布上的屏幕 y（px）。
   */
  originY: number
  /**
   * 每 mm 多少像素。
   */
  scale: number
  /**
   * 纸张宽度，mm。
   */
  pageWidth: number
  /**
   * 纸张高度，mm。
   */
  pageHeight: number
  /**
   * 选中元素的外框（屏幕 px），没有选中为 null。
   */
  highlight: { x: number; y: number; width: number; height: number } | null
  /**
   * 光标 x（mm），不在画布上为 null。
   */
  cursorX: number | null
  /**
   * 光标 y（mm），不在画布上为 null。
   */
  cursorY: number | null
}

/**
 * 标尺的颜色（取自当前主题的 CSS 变量）。
 */
export interface RulerColors {
  /**
   * 底色（纸张以外）。
   */
  background: string
  /**
   * 纸张范围的底色。
   */
  page: string
  /**
   * 选中元素范围的底色。
   */
  highlight: string
  /**
   * 刻度线。
   */
  tick: string
  /**
   * 刻度数字。
   */
  label: string
  /**
   * 光标位置线。
   */
  cursor: string
}

/**
 * 小刻度的候选间距，mm：取屏幕上不小于 5 像素的最小一档。
 */
const MINOR_STEPS = [0.5, 1, 2, 5, 10, 20, 50, 100]

/**
 * 大刻度（带数字）的候选间距，mm：取屏幕上不小于 44 像素的最小一档。
 */
const MAJOR_STEPS = [5, 10, 20, 50, 100, 200, 500]

/**
 * 画一条标尺：底色、纸张范围、选中范围、刻度、数字、光标线（按设备像素比画清晰）。
 * @param canvas 标尺的画布。
 * @param horizontal true 画上方的横向标尺，false 画左侧的竖向标尺。
 * @param view 视图状态。
 * @param colors 颜色。
 */
export function paintRuler(canvas: HTMLCanvasElement, horizontal: boolean, view: RulerView, colors: RulerColors): void {
  const ratio = window.devicePixelRatio || 1
  const width = canvas.clientWidth
  const height = canvas.clientHeight
  if (!width || !height) return
  // 画布的像素尺寸跟着 CSS 尺寸和设备像素比，高分屏上刻度也清晰。
  if (canvas.width !== Math.round(width * ratio) || canvas.height !== Math.round(height * ratio)) {
    canvas.width = Math.round(width * ratio)
    canvas.height = Math.round(height * ratio)
  }

  const context = canvas.getContext('2d')
  if (!context) return
  context.setTransform(ratio, 0, 0, ratio, 0, 0)
  context.fillStyle = colors.background
  context.fillRect(0, 0, width, height)

  // 横、竖两条标尺共用一套代码：沿标尺方向的是“长度”，垂直方向的是“厚度”。
  const origin = horizontal ? view.originX : view.originY
  const length = horizontal ? width : height
  const thickness = horizontal ? height : width
  const scale = view.scale
  const pageEnd = origin + (horizontal ? view.pageWidth : view.pageHeight) * scale
  context.fillStyle = colors.page
  if (horizontal) context.fillRect(origin, 0, pageEnd - origin, height)
  else context.fillRect(0, origin, width, pageEnd - origin)

  if (view.highlight) {
    context.fillStyle = colors.highlight
    if (horizontal) context.fillRect(view.highlight.x, 0, view.highlight.width, height)
    else context.fillRect(0, view.highlight.y, width, view.highlight.height)
  }

  // 按当前缩放选刻度间距：小刻度不密于 5 像素，带数字的大刻度不密于 44 像素。
  const minor = MINOR_STEPS.find((step) => step * scale >= 5) ?? 100
  const major = MAJOR_STEPS.find((step) => step * scale >= 44) ?? 500
  const end = (length - origin) / scale
  context.strokeStyle = colors.tick
  context.lineWidth = 1
  context.beginPath()
  // 从画布左（上）边对应的毫米数开始画，大刻度画满厚度，半格画 8 像素，其余 4 像素。
  for (let mm = Math.floor(-origin / scale / minor) * minor; mm <= end; mm += minor) {
    const position = Math.round(origin + mm * scale) + 0.5
    const isMajor = Math.abs(mm / major - Math.round(mm / major)) < 1e-6
    const isHalf = !isMajor && Math.abs(mm / (major / 2) - Math.round(mm / (major / 2))) < 1e-6
    const tick = isMajor ? thickness : isHalf ? 8 : 4
    if (horizontal) {
      context.moveTo(position, thickness)
      context.lineTo(position, thickness - tick)
    } else {
      context.moveTo(thickness, position)
      context.lineTo(thickness - tick, position)
    }
  }

  context.stroke()
  context.fillStyle = colors.label
  context.font = '10px Consolas, "Cascadia Code", monospace'
  context.textBaseline = 'top'
  // 大刻度旁边写毫米数；竖标尺的数字逆时针转 90°。
  for (let mm = Math.floor(-origin / scale / major) * major; mm <= end; mm += major) {
    const position = origin + mm * scale
    const label = String(Math.round(mm * 10) / 10)
    if (horizontal) {
      context.fillText(label, position + 3, 2)
    } else {
      const textWidth = context.measureText(label).width
      context.save()
      context.translate(2, position + 3 + textWidth)
      context.rotate(-Math.PI / 2)
      context.fillText(label, 0, 0)
      context.restore()
    }
  }

  // 光标位置画一条细线。
  const cursor = horizontal ? view.cursorX : view.cursorY
  if (cursor !== null) {
    const position = Math.round(origin + cursor * scale) + 0.5
    context.strokeStyle = colors.cursor
    context.beginPath()
    if (horizontal) {
      context.moveTo(position, 0)
      context.lineTo(position, height)
    } else {
      context.moveTo(0, position)
      context.lineTo(width, position)
    }

    context.stroke()
  }
}

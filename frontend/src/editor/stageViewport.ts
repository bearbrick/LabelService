/**
 * 设计画布的视口：Konva 舞台和图层、纸张和网格、缩放（适应窗口、按中心、按光标）、滚轮和拖动平移、纸面和屏幕坐标换算。
 * 选择、拖动、变换等编辑操作在子类 DesignerStage（stage.ts）里。
 */
import Konva from 'konva'
import type { LabelElement, LabelTemplate } from '../model/template.ts'
import { BASE_PX_PER_MM } from '../model/units.ts'
import type { DrawContext } from './draw.ts'
import { clamp } from './geometry.ts'
import { dragPan, drawGrid } from './overlays.ts'

/**
 * 画布的显示和操作设置（来自设计器 store）。
 */
export interface StageSettings {
  /**
   * 是否显示网格。
   */
  showGrid: boolean
  /**
   * 网格间距，mm。
   */
  gridSize: number
  /**
   * 拖动时是否吸附参考线和网格。
   */
  snap: boolean
  /**
   * 按住空格时只平移画布，不能拖元素。
   */
  panMode: boolean
}

/**
 * 画布和外部（设计器 store、画布组件）之间的接口：画布从这里读数据，通过回调通知变化。
 */
export interface StageHost {
  /**
   * 当前模板。
   * @returns 模板（画布只在拖动、变换结束时改坐标）。
   */
  template(): LabelTemplate
  /**
   * 当前选中的元素。
   * @returns 元素 ID。
   */
  selection(): readonly string[]
  /**
   * 改变选择。
   * @param ids 新选中的元素 ID。
   */
  select(ids: string[]): void
  /**
   * 当前的显示和操作设置。
   * @returns 设置。
   */
  settings(): StageSettings
  /**
   * 绘制元素用的上下文。
   * @returns 绘制上下文。
   */
  drawContext(): DrawContext
  /**
   * 显示方式、字段、DPI、字体或素材变化时改变，用来判断要不要全部重画。
   * @returns 签名文本。
   */
  contextSignature(): string
  /**
   * 拖动或变换改了坐标，记录一次撤销历史。
   */
  committed(): void
  /**
   * 光标在纸面上的位置变化（状态栏、标尺显示）。
   * @param x 光标 x，mm；离开画布时为 null。
   * @param y 光标 y，mm；离开画布时为 null。
   */
  pointer(x: number | null, y: number | null): void
  /**
   * 缩放或平移了（标尺、文本编辑框要跟着更新）。
   */
  viewChanged(): void
  /**
   * 双击了没锁定的元素（文本元素进入直接编辑）。
   * @param element 元素。
   */
  doubleClicked(element: LabelElement): void
  /**
   * 给用户的提示（如条码旋转角度被自动对齐）。
   * @param message 提示文字。
   */
  notify(message: string): void
}

/**
 * 最小缩放：实际大小的 25%。
 */
const MIN_SCALE = BASE_PX_PER_MM * 0.25

/**
 * 最大缩放：实际大小的 800%。
 */
const MAX_SCALE = BASE_PX_PER_MM * 8

/**
 * 画布视口：舞台坐标单位是 mm（舞台缩放 = 每 mm 的像素），舞台位置就是纸张原点在画布上的位置。
 */
export class StageViewport {
  /**
   * Konva 舞台。
   */
  readonly stage: Konva.Stage
  /**
   * 画布的外层容器（尺寸、滚轮和平移事件）。
   */
  protected readonly wrap: HTMLElement
  /**
   * 外部接口。
   */
  protected readonly host: StageHost
  /**
   * 元素层（快照导出按名字 elements 找它）。
   */
  protected readonly main = new Konva.Layer({ name: 'elements' })
  /**
   * 覆盖层：参考线、框选框、拖放高亮、变换框。
   */
  protected readonly overlay = new Konva.Layer()
  /**
   * 白色纸张。
   */
  protected readonly paper = new Konva.Rect({ fill: '#FFFFFF', name: 'paper' })
  /**
   * 销毁时要解除的事件监听。
   */
  protected readonly cleanup: Array<() => void> = []
  /**
   * 当前缩放：每 mm 多少像素。
   */
  protected scaleValue = BASE_PX_PER_MM
  /**
   * 主题颜色：强调色（选择框）和参考线颜色。
   */
  protected colors = { accent: '#1555d4', guide: '#e0457b' }
  /**
   * 下次尺寸可用时要适应窗口（容器还没有尺寸时先记下）。
   */
  private needsFit = true
  /**
   * 正在拖动平移。
   */
  private panning = false

  /**
   * 建舞台：背景层（纸张阴影、纸张、网格）、元素层、覆盖层；绑定滚轮和平移。
   * @param wrap 画布的外层容器。
   * @param container Konva 的容器。
   * @param host 外部接口。
   */
  constructor(wrap: HTMLElement, container: HTMLDivElement, host: StageHost) {
    this.wrap = wrap
    this.host = host
    this.readColors()
    this.stage = new Konva.Stage({ container, width: wrap.clientWidth || 10, height: wrap.clientHeight || 10 })
    const background = new Konva.Layer({ listening: true })
    const shadow = new Konva.Rect({ fill: '#FFFFFF', shadowColor: 'rgba(0,0,0,0.28)', shadowBlur: 2.5, shadowOffsetY: 0.4, listening: false, name: 'shadow' })
    background.add(shadow, this.paper, new Konva.Shape({ listening: false, sceneFunc: (context) => this.drawGrid(context) }))
    this.stage.add(background, this.main, this.overlay)
    this.stage.scale({ x: this.scaleValue, y: this.scaleValue })

    const onWheel = (event: WheelEvent) => this.onWheel(event)
    this.wrap.addEventListener('wheel', onWheel, { passive: false })
    const onPanStart = (event: PointerEvent) => this.onPanStart(event)
    this.wrap.addEventListener('pointerdown', onPanStart, true)
    this.cleanup.push(
      () => this.wrap.removeEventListener('wheel', onWheel),
      () => this.wrap.removeEventListener('pointerdown', onPanStart, true),
    )
  }

  /**
   * 每 mm 多少像素。
   * @returns 像素数。
   */
  get scale(): number {
    return this.scaleValue
  }

  /**
   * 缩放比例，1 表示实际大小。
   * @returns 缩放比例。
   */
  get zoom(): number {
    return this.scaleValue / BASE_PX_PER_MM
  }

  /**
   * 纸张原点在画布上的位置（px）。
   * @returns 屏幕坐标。
   */
  get origin(): { x: number; y: number } {
    return { x: this.stage.x(), y: this.stage.y() }
  }

  /**
   * 容器尺寸变化后调整舞台尺寸；还没适应过窗口时适应一次。
   */
  resize(): void {
    const width = this.wrap.clientWidth
    const height = this.wrap.clientHeight
    if (!width || !height) return
    this.stage.size({ width, height })
    if (this.needsFit) this.zoomFit()
    else this.afterView()
  }

  /**
   * 以画布中心为基点缩放。
   * @param factor 缩放倍数，如 1.25 放大、0.8 缩小。
   */
  zoomBy(factor: number): void {
    this.zoomAt(this.scaleValue * factor, this.stage.width() / 2, this.stage.height() / 2)
  }

  /**
   * 以画布中心为基点缩放到指定比例。
   * @param zoom 缩放比例，1 表示实际大小。
   */
  zoomTo(zoom: number): void {
    this.zoomAt(BASE_PX_PER_MM * zoom, this.stage.width() / 2, this.stage.height() / 2)
  }

  /**
   * 让整张纸居中放进可见区域（四周留 48 像素）。
   */
  zoomFit(): void {
    const page = this.host.template().page
    const width = this.stage.width()
    const height = this.stage.height()
    if (width < 40 || height < 40) {
      this.needsFit = true
      return
    }

    this.needsFit = false
    const scale = clamp(Math.min((width - 96) / page.width, (height - 96) / page.height), MIN_SCALE, MAX_SCALE)
    this.apply(scale, (width - page.width * scale) / 2, (height - page.height * scale) / 2)
  }

  /**
   * 下次尺寸可用时重新适应窗口（换模板时用）。
   */
  requestFit(): void {
    this.needsFit = true
    this.resize()
  }

  /**
   * 视图中心对应的纸面坐标（mm），新元素放在这里。
   * @returns 纸面坐标。
   */
  viewCenter(): { x: number; y: number } {
    return { x: (this.stage.width() / 2 - this.stage.x()) / this.scaleValue, y: (this.stage.height() / 2 - this.stage.y()) / this.scaleValue }
  }

  /**
   * 纸面坐标（mm）换算成画布上的位置（px）。
   * @param x 纸面 x，mm。
   * @param y 纸面 y，mm。
   * @returns 屏幕坐标。
   */
  toScreen(x: number, y: number): { x: number; y: number } {
    return { x: this.stage.x() + x * this.scaleValue, y: this.stage.y() + y * this.scaleValue }
  }

  /**
   * 拖放事件对应的纸面坐标（mm）。
   * @param event 拖放事件。
   * @returns 纸面坐标；不在画布上时为 null。
   */
  pointerMm(event: DragEvent): { x: number; y: number } | null {
    this.stage.setPointersPositions(event)
    return this.main.getRelativePointerPosition()
  }

  /**
   * 页面主题变化后重新读取颜色（CSS 变量 --blue、--guide）。
   */
  readColors(): void {
    const style = getComputedStyle(document.documentElement)
    this.colors = {
      accent: style.getPropertyValue('--blue').trim() || '#1555d4',
      guide: style.getPropertyValue('--guide').trim() || '#e0457b',
    }
  }

  /**
   * 解除事件监听，销毁舞台。
   */
  destroy(): void {
    this.cleanup.splice(0).forEach((dispose) => dispose())
    this.stage.destroy()
  }

  /**
   * 以画布上的某一点为基点缩放：缩放前后这一点对应的纸面位置不变。
   * @param scale 新的缩放（每 mm 像素），超出范围时取边界。
   * @param x 基点的屏幕 x。
   * @param y 基点的屏幕 y。
   */
  protected zoomAt(scale: number, x: number, y: number): void {
    const next = clamp(scale, MIN_SCALE, MAX_SCALE)
    const mmX = (x - this.stage.x()) / this.scaleValue
    const mmY = (y - this.stage.y()) / this.scaleValue
    this.apply(next, x - mmX * next, y - mmY * next)
  }

  /**
   * 视图变化后重画并通知外部；子类在这里调整随缩放变化的覆盖层。
   */
  protected afterView(): void {
    this.stage.batchDraw()
    this.host.viewChanged()
  }

  /**
   * 设置缩放和位置。
   * @param scale 每 mm 像素。
   * @param x 纸张原点的屏幕 x。
   * @param y 纸张原点的屏幕 y。
   */
  private apply(scale: number, x: number, y: number): void {
    this.scaleValue = scale
    this.stage.scale({ x: scale, y: scale })
    this.stage.position({ x, y })
    this.afterView()
  }

  /**
   * 画网格（背景层的自定义图形每次重画时调用）。
   * @param context Konva 的绘图上下文。
   */
  private drawGrid(context: Konva.Context): void {
    const settings = this.host.settings()
    if (settings.showGrid) drawGrid(context._context, this.host.template().page, settings.gridSize, this.scaleValue)
  }

  /**
   * Ctrl+滚轮缩放，滚轮上下平移，Shift+滚轮左右平移。
   * @param event 滚轮事件。
   */
  private onWheel(event: WheelEvent): void {
    event.preventDefault()
    if (event.ctrlKey || event.metaKey) {
      const rect = this.wrap.getBoundingClientRect()
      this.zoomAt(this.scaleValue * Math.exp(-event.deltaY * 0.0018), event.clientX - rect.left, event.clientY - rect.top)
      return
    }

    const dx = event.shiftKey ? event.deltaY : event.deltaX
    const dy = event.shiftKey ? 0 : event.deltaY
    this.stage.position({ x: this.stage.x() - dx, y: this.stage.y() - dy })
    this.afterView()
  }

  /**
   * 空格 + 拖动，或按住鼠标中键拖动，平移画布。
   * @param event 按下指针的事件（捕获阶段，先于 Konva 处理）。
   */
  private onPanStart(event: PointerEvent): void {
    if (!(this.host.settings().panMode || event.button === 1) || this.panning) return
    event.preventDefault()
    event.stopPropagation()
    this.panning = true
    const move = (x: number, y: number) => {
      this.stage.position({ x, y })
      this.afterView()
    }
    dragPan(event, this.stage.position(), move, () => (this.panning = false))
  }
}

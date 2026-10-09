/**
 * 设计画布：管理 Konva 舞台、元素节点、选择框、拖动吸附、缩放平移。
 * 模板数据由外部的 store 持有，这里只读数据、在拖动或变换结束时改坐标，再通知外部记录撤销历史。
 * 每个元素记一份“内容签名”，只有内容变了才重画，100 个元素时拖动也流畅。
 */
import Konva from 'konva'
import type { LabelElement, LabelTemplate } from '../model/template.ts'
import { BASE_PX_PER_MM, normalizeRotation, roundMm } from '../model/units.ts'
import { drawElement, setGeometry } from './draw.ts'
import type { DrawContext } from './draw.ts'
import { boxFromPoints, clamp, contains, elementBounds } from './geometry.ts'
import { dragPan, drawGrid, transformRules } from './overlays.ts'
import type { Box } from './geometry.ts'
import { snapBox } from './snapping.ts'

export interface StageSettings {
  showGrid: boolean
  gridSize: number
  snap: boolean
  /** 按住空格时只平移画布，不能拖元素。 */
  panMode: boolean
}

export interface StageHost {
  template(): LabelTemplate
  selection(): readonly string[]
  select(ids: string[]): void
  settings(): StageSettings
  drawContext(): DrawContext
  /** 显示方式、字段、DPI、字体或素材变化时改变，用来判断要不要全部重画。 */
  contextSignature(): string
  /** 拖动或变换改了坐标，记录一次撤销历史。 */
  committed(): void
  pointer(x: number | null, y: number | null): void
  viewChanged(): void
  doubleClicked(element: LabelElement): void
  notify(message: string): void
}

const MIN_SCALE = BASE_PX_PER_MM * 0.25
const MAX_SCALE = BASE_PX_PER_MM * 8

export class DesignerStage {
  readonly stage: Konva.Stage
  private readonly wrap: HTMLElement
  private readonly host: StageHost
  private readonly main = new Konva.Layer({ name: 'elements' })
  private readonly overlay = new Konva.Layer()
  private readonly paper = new Konva.Rect({ fill: '#FFFFFF', name: 'paper' })
  private readonly transformer: Konva.Transformer
  private readonly guides = new Konva.Group({ listening: false })
  private readonly marquee = new Konva.Rect({ visible: false, listening: false })
  private readonly dropBox = new Konva.Rect({ visible: false, listening: false })
  private readonly nodes = new Map<string, Konva.Group>()
  private readonly signatures = new Map<string, string>()
  private readonly cleanup: Array<() => void> = []
  private scaleValue = BASE_PX_PER_MM
  private needsFit = true
  private marqueeStart: { x: number; y: number; additive: boolean; base: string[] } | null = null
  private panning = false
  private transformMode: 'box' | 'hline' | 'vline' = 'box'
  private editingId: string | null = null
  private colors = { accent: '#1555d4', guide: '#e0457b' }

  constructor(wrap: HTMLElement, container: HTMLDivElement, host: StageHost) {
    this.wrap = wrap
    this.host = host
    this.readColors()
    this.stage = new Konva.Stage({ container, width: wrap.clientWidth || 10, height: wrap.clientHeight || 10 })
    const background = new Konva.Layer({ listening: true })
    const shadow = new Konva.Rect({ fill: '#FFFFFF', shadowColor: 'rgba(0,0,0,0.28)', shadowBlur: 2.5, shadowOffsetY: 0.4, listening: false, name: 'shadow' })
    background.add(shadow, this.paper, new Konva.Shape({ listening: false, sceneFunc: (context) => this.drawGrid(context) }))
    this.transformer = new Konva.Transformer({
      rotateAnchorOffset: 24,
      anchorSize: 8,
      anchorCornerRadius: 2,
      anchorFill: '#FFFFFF',
      borderStrokeWidth: 1.2,
      ignoreStroke: true,
      flipEnabled: false,
      rotationSnaps: [0, 45, 90, 135, 180, 225, 270, 315],
      rotationSnapTolerance: 4,
      boundBoxFunc: (oldBox, newBox) => {
        if (this.transformMode !== 'vline' && Math.abs(newBox.width) < 6) return oldBox
        if (this.transformMode !== 'hline' && Math.abs(newBox.height) < 6) return oldBox
        return newBox
      },
    })
    this.overlay.add(this.guides, this.marquee, this.dropBox, this.transformer)
    this.stage.add(background, this.main, this.overlay)
    this.stage.scale({ x: this.scaleValue, y: this.scaleValue })
    this.applyColors()
    this.bindEvents()
  }

  /** 每 mm 多少像素。 */
  get scale(): number {
    return this.scaleValue
  }

  /** 缩放比例，1 表示实际大小。 */
  get zoom(): number {
    return this.scaleValue / BASE_PX_PER_MM
  }

  /** 纸张原点在画布上的位置（px）。 */
  get origin(): { x: number; y: number } {
    return { x: this.stage.x(), y: this.stage.y() }
  }

  /** 按模板同步节点：新建、删除、重画内容变化的元素、调整叠放顺序。 */
  sync(): void {
    const template = this.host.template()
    const context = this.host.drawContext()
    const signature = this.host.contextSignature()
    const settings = this.host.settings()
    const seen = new Set<string>()
    template.elements.forEach((element, index) => {
      seen.add(element.id)
      const group = this.nodes.get(element.id) ?? this.createNode(element)
      if (!group.isDragging()) setGeometry(group, element)
      group.visible(!element.hidden && element.id !== this.editingId)
      group.draggable(!element.locked && !settings.panMode)
      const elementSignature = JSON.stringify(element) + signature
      if (this.signatures.get(element.id) !== elementSignature) {
        drawElement(group, element, context)
        this.signatures.set(element.id, elementSignature)
      }

      group.zIndex(index)
    })

    for (const [id, group] of [...this.nodes]) {
      if (seen.has(id)) continue
      group.destroy()
      this.nodes.delete(id)
      this.signatures.delete(id)
    }

    const page = template.page
    this.paper.size({ width: page.width, height: page.height })
    this.stage.findOne('.shadow')?.size({ width: page.width, height: page.height })
    this.refreshSelection()
    this.stage.batchDraw()
  }

  /** 按当前选择更新变换框：线只能拉长度，二维码和图标等比缩放，锁定的元素不能变换。 */
  refreshSelection(): void {
    const template = this.host.template()
    const selected = this.host.selection()
      .map((id) => template.elements.find((element) => element.id === id))
      .filter((element): element is LabelElement => !!element && !element.hidden && element.id !== this.editingId)
    const nodes = selected.map((element) => this.nodes.get(element.id)).filter((node): node is Konva.Group => !!node)
    const rules = transformRules(selected)
    this.transformMode = rules.mode
    this.transformer.setAttrs({
      enabledAnchors: rules.locked ? [] : rules.anchors,
      rotateEnabled: rules.rotate && !rules.locked,
      resizeEnabled: !rules.locked,
      keepRatio: rules.keepRatio,
      borderDash: rules.locked ? [4, 3] : undefined,
    })
    this.transformer.nodes(nodes)
    if (nodes.length) this.transformer.forceUpdate()
    this.overlay.batchDraw()
  }

  resize(): void {
    const width = this.wrap.clientWidth
    const height = this.wrap.clientHeight
    if (!width || !height) return
    this.stage.size({ width, height })
    if (this.needsFit) this.zoomFit()
    else this.afterView()
  }

  zoomBy(factor: number): void {
    this.zoomAt(this.scaleValue * factor, this.stage.width() / 2, this.stage.height() / 2)
  }

  zoomTo(zoom: number): void {
    this.zoomAt(BASE_PX_PER_MM * zoom, this.stage.width() / 2, this.stage.height() / 2)
  }

  /** 让整张纸居中放进可见区域。 */
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

  /** 下次尺寸可用时重新适应窗口（换模板时用）。 */
  requestFit(): void {
    this.needsFit = true
    this.resize()
  }

  /** 视图中心对应的纸面坐标（mm），新元素放在这里。 */
  viewCenter(): { x: number; y: number } {
    return { x: (this.stage.width() / 2 - this.stage.x()) / this.scaleValue, y: (this.stage.height() / 2 - this.stage.y()) / this.scaleValue }
  }

  /** 纸面坐标（mm）换算成画布上的位置（px）。 */
  toScreen(x: number, y: number): { x: number; y: number } {
    return { x: this.stage.x() + x * this.scaleValue, y: this.stage.y() + y * this.scaleValue }
  }

  /** 选中元素的屏幕外框，给标尺高亮用。 */
  selectionScreenBox(): Box | null {
    return this.transformer.nodes().length ? this.transformer.getClientRect() : null
  }

  /** 拖放事件对应的纸面坐标（mm）。 */
  pointerMm(event: DragEvent): { x: number; y: number } | null {
    this.stage.setPointersPositions(event)
    return this.main.getRelativePointerPosition()
  }

  /** 拖放事件下方的元素 ID。 */
  elementAt(event: DragEvent): string | null {
    this.stage.setPointersPositions(event)
    const position = this.stage.getPointerPosition()
    const shape = position ? this.main.getIntersection(position) : null
    return shape?.findAncestor('.element', true)?.id() ?? null
  }

  /** 拖字段到条码上时高亮目标元素；传 null 取消。 */
  highlight(box: Box | null): void {
    this.dropBox.setAttrs(box
      ? { ...box, width: Math.max(box.width, 1), height: Math.max(box.height, 1), stroke: this.colors.accent, strokeWidth: 2 / this.scaleValue, fill: 'rgba(21,85,212,0.09)', visible: true }
      : { visible: false })
    this.overlay.batchDraw()
  }

  /** 正在直接编辑文本的元素在画布上隐藏，由外部的输入框代替。 */
  setEditing(id: string | null): void {
    this.editingId = id
    this.sync()
  }

  /** 页面主题变化后重新读取颜色。 */
  readColors(): void {
    const style = getComputedStyle(document.documentElement)
    this.colors = {
      accent: style.getPropertyValue('--blue').trim() || '#1555d4',
      guide: style.getPropertyValue('--guide').trim() || '#e0457b',
    }
  }

  destroy(): void {
    this.cleanup.splice(0).forEach((dispose) => dispose())
    this.stage.destroy()
  }

  private applyColors(): void {
    this.transformer.setAttrs({ borderStroke: this.colors.accent, anchorStroke: this.colors.accent })
  }

  private createNode(element: LabelElement): Konva.Group {
    const group = new Konva.Group({ id: element.id, name: 'element' })
    group.on('dragstart', () => this.onDragStart(group))
    group.on('dragmove', (event) => this.onDragMove(group, event.evt as MouseEvent))
    group.on('dragend', () => this.onDragEnd(group))
    group.on('transform', () => this.onTransform(group))
    group.on('transformend', () => this.onTransformEnd(group))
    group.on('mouseenter', () => {
      const current = this.find(group.id())
      if (!this.host.settings().panMode) this.stage.container().style.cursor = current?.locked ? 'default' : 'move'
    })
    group.on('mouseleave', () => {
      this.stage.container().style.cursor = ''
    })
    this.main.add(group)
    this.nodes.set(element.id, group)
    return group
  }

  private find(id: string): LabelElement | undefined {
    return this.host.template().elements.find((element) => element.id === id)
  }

  private zoomAt(scale: number, x: number, y: number): void {
    const next = clamp(scale, MIN_SCALE, MAX_SCALE)
    const mmX = (x - this.stage.x()) / this.scaleValue
    const mmY = (y - this.stage.y()) / this.scaleValue
    this.apply(next, x - mmX * next, y - mmY * next)
  }

  private apply(scale: number, x: number, y: number): void {
    this.scaleValue = scale
    this.stage.scale({ x: scale, y: scale })
    this.stage.position({ x, y })
    this.afterView()
  }

  private afterView(): void {
    this.marquee.strokeWidth(1 / this.scaleValue)
    this.guides.destroyChildren()
    this.stage.batchDraw()
    this.host.viewChanged()
  }

  private drawGrid(context: Konva.Context): void {
    const settings = this.host.settings()
    if (settings.showGrid) drawGrid(context._context, this.host.template().page, settings.gridSize, this.scaleValue)
  }

  private bindEvents(): void {
    this.stage.on('mousedown touchstart', (event) => this.onPointerDown(event))
    this.stage.on('mousemove touchmove', () => {
      const position = this.main.getRelativePointerPosition()
      if (!position) return
      this.host.pointer(position.x, position.y)
      if (this.marqueeStart) {
        this.marquee.setAttrs(boxFromPoints(this.marqueeStart.x, this.marqueeStart.y, position.x, position.y))
        this.overlay.batchDraw()
      }
    })
    this.stage.on('mouseleave', () => this.host.pointer(null, null))
    this.stage.on('dblclick dbltap', (event) => {
      const id = event.target.findAncestor('.element', true)?.id()
      const element = id ? this.find(id) : undefined
      if (element && !element.locked) this.host.doubleClicked(element)
    })

    const endMarquee = () => this.endMarquee()
    window.addEventListener('mouseup', endMarquee)
    window.addEventListener('touchend', endMarquee)
    const onWheel = (event: WheelEvent) => this.onWheel(event)
    this.wrap.addEventListener('wheel', onWheel, { passive: false })
    const onPanStart = (event: PointerEvent) => this.onPanStart(event)
    this.wrap.addEventListener('pointerdown', onPanStart, true)
    this.cleanup.push(
      () => window.removeEventListener('mouseup', endMarquee),
      () => window.removeEventListener('touchend', endMarquee),
      () => this.wrap.removeEventListener('wheel', onWheel),
      () => this.wrap.removeEventListener('pointerdown', onPanStart, true),
    )
  }

  private onPointerDown(event: Konva.KonvaEventObject<MouseEvent | TouchEvent>): void {
    const mouse = event.evt as MouseEvent
    if (mouse.button === 1 || mouse.button === 2 || this.host.settings().panMode) return
    if (event.target.getParent()?.getClassName() === 'Transformer') return
    this.wrap.focus({ preventScroll: true })
    const additive = mouse.shiftKey || mouse.ctrlKey || mouse.metaKey
    const id = event.target.findAncestor('.element', true)?.id()
    const selection = [...this.host.selection()]
    if (id) {
      if (additive) this.host.select(selection.includes(id) ? selection.filter((item) => item !== id) : [...selection, id])
      else if (!selection.includes(id)) this.host.select([id])
      return
    }

    const position = this.main.getRelativePointerPosition()
    if (!position) return
    this.marqueeStart = { x: position.x, y: position.y, additive, base: selection }
    this.marquee.setAttrs({ x: position.x, y: position.y, width: 0, height: 0, visible: true, stroke: this.colors.accent, strokeWidth: 1 / this.scaleValue, fill: 'rgba(21,85,212,0.07)' })
    if (!additive) this.host.select([])
  }

  private endMarquee(): void {
    const start = this.marqueeStart
    if (!start) return
    this.marqueeStart = null
    const box: Box = { x: this.marquee.x(), y: this.marquee.y(), width: this.marquee.width(), height: this.marquee.height() }
    this.marquee.visible(false)
    this.overlay.batchDraw()
    if (box.width * this.scaleValue < 3 && box.height * this.scaleValue < 3) return
    const hits = this.host.template().elements
      .filter((element) => !element.hidden && contains(box, elementBounds(element)))
      .map((element) => element.id)
    this.host.select(start.additive ? [...new Set([...start.base, ...hits])] : hits)
  }

  private onDragStart(group: Konva.Group): void {
    if (this.host.settings().panMode) {
      group.stopDrag()
      return
    }

    if (!this.host.selection().includes(group.id())) this.host.select([group.id()])
  }

  /** 拖动中吸附到参考线或网格；按住 Alt 暂时不吸附。 */
  private onDragMove(group: Konva.Group, event: MouseEvent): void {
    this.guides.destroyChildren()
    const settings = this.host.settings()
    const element = this.find(group.id())
    if (!element || !settings.snap || event?.altKey) {
      this.overlay.batchDraw()
      this.host.viewChanged()
      return
    }

    const template = this.host.template()
    const selection = this.host.selection()
    const current = elementBounds({ ...element, x: group.x() - element.width / 2, y: group.y() - element.height / 2 })
    const others = template.elements
      .filter((other) => !other.hidden && other.id !== element.id && !selection.includes(other.id))
      .map(elementBounds)
    const snap = snapBox(current, others, template.page, { threshold: 5 / this.scaleValue, grid: settings.gridSize })
    group.position({ x: group.x() + snap.dx, y: group.y() + snap.dy })
    const width = 1 / this.scaleValue
    const dash = [4 / this.scaleValue, 3 / this.scaleValue]
    const page = template.page
    if (snap.guideX !== null) this.guides.add(new Konva.Line({ points: [snap.guideX, -6, snap.guideX, page.height + 6], stroke: this.colors.guide, strokeWidth: width, dash }))
    if (snap.guideY !== null) this.guides.add(new Konva.Line({ points: [-6, snap.guideY, page.width + 6, snap.guideY], stroke: this.colors.guide, strokeWidth: width, dash }))
    this.overlay.batchDraw()
    this.host.viewChanged()
  }

  /** 选中的元素一起移动（变换框会带着其他选中节点），结束时统一写回坐标。 */
  private onDragEnd(group: Konva.Group): void {
    this.guides.destroyChildren()
    this.overlay.batchDraw()
    const ids = new Set([...this.host.selection(), group.id()])
    for (const id of ids) {
      const node = this.nodes.get(id)
      const element = this.find(id)
      if (!node || !element || element.locked) continue
      element.x = roundMm(node.x() - element.width / 2)
      element.y = roundMm(node.y() - element.height / 2)
    }

    this.host.committed()
  }

  /** 把缩放折算成宽高，保持节点 scale 为 1，元素内容按新尺寸重画。 */
  private onTransform(group: Konva.Group): void {
    const element = this.find(group.id())
    if (!element) return
    const width = element.width === 0 ? 0 : Math.max(0.5, element.width * group.scaleX())
    const height = element.height === 0 ? 0 : Math.max(0.5, element.height * group.scaleY())
    const centerX = group.x()
    const centerY = group.y()
    group.scale({ x: 1, y: 1 })
    element.width = roundMm(width)
    element.height = roundMm(height)
    element.rotation = normalizeRotation(group.rotation())
    element.x = roundMm(centerX - element.width / 2)
    element.y = roundMm(centerY - element.height / 2)
    setGeometry(group, element)
    drawElement(group, element, this.host.drawContext())
    this.signatures.set(element.id, JSON.stringify(element) + this.host.contextSignature())
  }

  private onTransformEnd(group: Konva.Group): void {
    const element = this.find(group.id())
    if (element?.type === 'barcode' && (element.rotation ?? 0) % 90) {
      element.rotation = (Math.round((element.rotation ?? 0) / 90) * 90) % 360
      setGeometry(group, element)
      this.host.notify('条码只能旋转 0°、90°、180°、270°，已自动对齐')
    }

    this.host.committed()
  }

  /** Ctrl+滚轮缩放，滚轮上下平移，Shift+滚轮左右平移。 */
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

  /** 空格 + 拖动，或按住鼠标中键拖动，平移画布。 */
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

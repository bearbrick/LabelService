/**
 * 拼版设计器的画布：纸张、格子（显示模板预览的位图和序号）、选择、拖动吸附、从左侧拖入模板、缩放平移。
 * 版式数据由 stores/sheet.ts 持有，这里只读；拖动结束时把新位置交给 host，由 store 记录撤销历史。
 * 格子里画的是服务端渲染好的缩略图（一张位图），24 个格子、每格 20 个元素拖动时也只是移动位图。
 */
import Konva from 'konva'
import { footprint, slotBox } from '../model/layout.ts'
import type { Box, SheetLayout, SheetSlot } from '../model/layout.ts'
import { BASE_PX_PER_MM, roundMm } from '../model/units.ts'
import { unionBox } from './geometry.ts'
import { drawGrid } from './overlays.ts'
import { snapBox } from './snapping.ts'

export interface SheetSettings {
  showGrid: boolean
  /** 网格间距（mm）。 */
  grid: number
  snap: boolean
}

export interface SheetStageHost {
  layout(): SheetLayout | null
  selection(): readonly number[]
  select(indices: number[]): void
  /** 越界或重叠的格子，标红。 */
  bad(): ReadonlySet<number>
  /** 格子里显示的模板预览，没有时只画框。 */
  image(): HTMLImageElement | null
  settings(): SheetSettings
  /** 拖动结束：这些格子移到了新位置（mm，已取 0.1）。 */
  moved(updates: Array<{ index: number; x: number; y: number }>): void
  /** 从左侧拖入模板，落点是纸上的坐标（mm）。 */
  dropped(x: number, y: number): void
}

const MIN_SCALE = BASE_PX_PER_MM * 0.15
const MAX_SCALE = BASE_PX_PER_MM * 6

export class SheetStage {
  readonly stage: Konva.Stage
  private readonly wrap: HTMLElement
  private readonly host: SheetStageHost
  private readonly paper = new Konva.Rect({ fill: '#FFFFFF', shadowColor: 'rgba(0,0,0,0.28)', shadowBlur: 3, shadowOffsetY: 0.5, name: 'paper' })
  private readonly grid = new Konva.Shape({ listening: false, sceneFunc: (context) => this.drawGrid(context) })
  private readonly slotsLayer = new Konva.Layer()
  private readonly guides = new Konva.Group({ listening: false })
  private readonly nodes: Konva.Group[] = []
  private readonly cleanup: Array<() => void> = []
  private scaleValue = BASE_PX_PER_MM
  private needsFit = true
  private dragStart: Map<number, { x: number; y: number }> | null = null
  private colors = { accent: '#1555d4', guide: '#e0457b', danger: '#d43a3a', line: '#9aa4b2' }

  constructor(wrap: HTMLElement, container: HTMLDivElement, host: SheetStageHost) {
    this.wrap = wrap
    this.host = host
    const style = getComputedStyle(document.documentElement)
    const read = (name: string, fallback: string) => style.getPropertyValue(name).trim() || fallback
    this.colors = { accent: read('--accent', this.colors.accent), guide: read('--guide', this.colors.guide), danger: read('--danger', this.colors.danger), line: read('--line-strong', this.colors.line) }
    this.stage = new Konva.Stage({ container, width: wrap.clientWidth || 10, height: wrap.clientHeight || 10 })
    const background = new Konva.Layer()
    background.add(this.paper, this.grid)
    const overlay = new Konva.Layer({ listening: false })
    overlay.add(this.guides)
    this.stage.add(background, this.slotsLayer, overlay)
    this.bindEvents(container)
  }

  /** 缩放比例，1 表示实际大小。 */
  get zoom(): number {
    return this.scaleValue / BASE_PX_PER_MM
  }

  /** 版式或选择变化后重画全部格子（最多 500 个，重建很快）。 */
  render(): void {
    const layout = this.host.layout()
    // 拖动中只移动位图，松开后由 store 改数据再整体重画。
    if (!layout || this.dragStart) return
    this.paper.size({ width: layout.paper.width, height: layout.paper.height })
    const selected = new Set(this.host.selection())
    const bad = this.host.bad()
    while (this.nodes.length > layout.slots.length) this.nodes.pop()!.destroy()
    layout.slots.forEach((slot, index) => {
      let node = this.nodes[index]
      if (!node) {
        node = new Konva.Group({ draggable: true })
        this.bindSlot(node, index)
        this.nodes.push(node)
        this.slotsLayer.add(node)
      }

      this.drawSlot(node, layout, slot, index, selected.has(index), bad.has(index))
    })
    if (this.needsFit) this.fit()
    this.stage.batchDraw()
  }

  /** 整张纸放进可见区域。 */
  fit(): void {
    const layout = this.host.layout()
    if (!layout || !this.wrap.clientWidth) return
    this.needsFit = false
    const scale = Math.min((this.wrap.clientWidth - 48) / layout.paper.width, (this.wrap.clientHeight - 48) / layout.paper.height)
    this.setScale(Math.max(MIN_SCALE, Math.min(MAX_SCALE, scale)))
    this.stage.position({ x: (this.wrap.clientWidth - layout.paper.width * this.scaleValue) / 2, y: (this.wrap.clientHeight - layout.paper.height * this.scaleValue) / 2 })
    this.stage.batchDraw()
  }

  zoomTo(zoom: number): void {
    const center = { x: this.wrap.clientWidth / 2, y: this.wrap.clientHeight / 2 }
    this.zoomAt(zoom * BASE_PX_PER_MM, center)
  }

  resize(): void {
    this.stage.size({ width: this.wrap.clientWidth || 10, height: this.wrap.clientHeight || 10 })
    if (this.needsFit) this.fit()
    this.stage.batchDraw()
  }

  /** 屏幕上的点换成纸上的坐标（mm）。 */
  toPaper(clientX: number, clientY: number): { x: number; y: number } {
    const rect = this.stage.container().getBoundingClientRect()
    return { x: (clientX - rect.left - this.stage.x()) / this.scaleValue, y: (clientY - rect.top - this.stage.y()) / this.scaleValue }
  }

  destroy(): void {
    for (const dispose of this.cleanup) dispose()
    this.stage.destroy()
  }

  private drawSlot(node: Konva.Group, layout: SheetLayout, slot: SheetSlot, index: number, selected: boolean, bad: boolean): void {
    const size = footprint(layout.slot, slot)
    const px = 1 / this.scaleValue
    node.position({ x: slot.x, y: slot.y })
    node.destroyChildren()
    const image = this.host.image()
    node.add(new Konva.Rect({ width: size.width, height: size.height, fill: bad ? 'rgba(212,58,58,0.10)' : '#FFFFFF' }))
    if (image) {
      // 和服务端一样：顺时针旋转后仍落在格子所占的矩形里。
      const offset = { 0: { x: 0, y: 0 }, 90: { x: layout.slot.height, y: 0 }, 180: { x: layout.slot.width, y: layout.slot.height }, 270: { x: 0, y: layout.slot.width } }[slot.rotation] ?? { x: 0, y: 0 }
      node.add(new Konva.Image({ image, ...offset, width: layout.slot.width, height: layout.slot.height, rotation: slot.rotation, opacity: bad ? 0.55 : 1, listening: false }))
    }

    node.add(new Konva.Rect({
      width: size.width,
      height: size.height,
      stroke: bad ? this.colors.danger : selected ? this.colors.accent : this.colors.line,
      strokeWidth: (selected || bad ? 2 : 1) * px,
      dash: selected || bad ? undefined : [4 * px, 3 * px],
      listening: false,
    }))
    const badge = Math.max(5, Math.min(size.width, size.height) * 0.16)
    node.add(new Konva.Circle({ x: badge * 0.75, y: badge * 0.75, radius: badge / 2, fill: bad ? this.colors.danger : selected ? this.colors.accent : '#4a5568', opacity: 0.9, listening: false }))
    node.add(new Konva.Text({ x: badge * 0.25, y: badge * 0.25 + badge * 0.12, width: badge, align: 'center', text: String(index + 1), fontSize: badge * 0.62, fontStyle: 'bold', fill: '#FFFFFF', listening: false }))
  }

  private bindSlot(node: Konva.Group, index: number): void {
    node.on('mousedown touchstart', (event) => {
      const additive = event.evt.shiftKey || (event.evt as MouseEvent).ctrlKey
      const current = this.host.selection()
      if (additive) this.host.select(current.includes(index) ? current.filter((item) => item !== index) : [...current, index])
      else if (!current.includes(index)) this.host.select([index])
    })
    node.on('dragstart', () => {
      const layout = this.host.layout()
      if (!layout) return
      if (!this.host.selection().includes(index)) this.host.select([index])
      this.dragStart = new Map(this.host.selection().map((item) => [item, { x: layout.slots[item].x, y: layout.slots[item].y }]))
    })
    node.on('dragmove', () => this.dragMove(node, index))
    node.on('dragend', () => {
      const start = this.dragStart
      this.dragStart = null
      this.guides.destroyChildren()
      if (!start) return
      this.host.moved([...start.keys()].map((item) => ({ index: item, x: roundMm(this.nodes[item].x()), y: roundMm(this.nodes[item].y()) })))
    })
  }

  /** 拖动中：选中的格子一起动，整体对齐到其他格子和纸边，或者吸附网格。 */
  private dragMove(node: Konva.Group, index: number): void {
    const layout = this.host.layout()
    const start = this.dragStart
    if (!layout || !start) return
    const origin = start.get(index)!
    let dx = node.x() - origin.x
    let dy = node.y() - origin.y
    const settings = this.host.settings()
    this.guides.destroyChildren()
    if (settings.snap) {
      const moving = [...start.entries()].map(([item, position]) => ({ ...slotBox(layout.slot, layout.slots[item]), x: position.x + dx, y: position.y + dy }))
      const others: Box[] = layout.slots.filter((_, item) => !start.has(item)).map((slot) => slotBox(layout.slot, slot))
      const snap = snapBox(unionBox(moving)!, others, layout.paper, { threshold: 6 / this.scaleValue, grid: settings.grid || null })
      dx += snap.dx
      dy += snap.dy
      this.drawGuides(layout, snap.guideX, snap.guideY)
    }

    for (const [item, position] of start) this.nodes[item].position({ x: position.x + dx, y: position.y + dy })
  }

  private drawGuides(layout: SheetLayout, x: number | null, y: number | null): void {
    const width = 1 / this.scaleValue
    if (x !== null) this.guides.add(new Konva.Line({ points: [x, -5, x, layout.paper.height + 5], stroke: this.colors.guide, strokeWidth: width, dash: [4 * width, 3 * width] }))
    if (y !== null) this.guides.add(new Konva.Line({ points: [-5, y, layout.paper.width + 5, y], stroke: this.colors.guide, strokeWidth: width, dash: [4 * width, 3 * width] }))
  }

  private drawGrid(context: Konva.Context): void {
    const layout = this.host.layout()
    const settings = this.host.settings()
    if (!layout || !settings.showGrid || !settings.grid) return
    drawGrid(context._context, layout.paper, settings.grid, this.scaleValue)
  }

  private setScale(scale: number): void {
    this.scaleValue = scale
    this.stage.scale({ x: scale, y: scale })
    this.paper.shadowBlur(3 / scale)
    this.render()
  }

  private zoomAt(scale: number, point: { x: number; y: number }): void {
    const next = Math.max(MIN_SCALE, Math.min(MAX_SCALE, scale))
    const paperPoint = { x: (point.x - this.stage.x()) / this.scaleValue, y: (point.y - this.stage.y()) / this.scaleValue }
    this.needsFit = false
    this.setScale(next)
    this.stage.position({ x: point.x - paperPoint.x * next, y: point.y - paperPoint.y * next })
    this.stage.batchDraw()
  }

  private bindEvents(container: HTMLDivElement): void {
    this.stage.on('mousedown touchstart', (event) => {
      if (event.target === this.stage || event.target === this.paper) this.host.select([])
    })
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      if (event.ctrlKey || event.metaKey) {
        const rect = container.getBoundingClientRect()
        this.zoomAt(this.scaleValue * (event.deltaY < 0 ? 1.1 : 1 / 1.1), { x: event.clientX - rect.left, y: event.clientY - rect.top })
        return
      }

      this.stage.position({ x: this.stage.x() - event.deltaX, y: this.stage.y() - event.deltaY })
      this.stage.batchDraw()
    }

    const onDragOver = (event: DragEvent) => event.preventDefault()
    const onDrop = (event: DragEvent) => {
      event.preventDefault()
      const point = this.toPaper(event.clientX, event.clientY)
      this.host.dropped(point.x, point.y)
    }

    const observer = new ResizeObserver(() => this.resize())
    observer.observe(this.wrap)
    container.addEventListener('wheel', onWheel, { passive: false })
    container.addEventListener('dragover', onDragOver)
    container.addEventListener('drop', onDrop)
    this.cleanup.push(
      () => observer.disconnect(),
      () => container.removeEventListener('wheel', onWheel),
      () => container.removeEventListener('dragover', onDragOver),
      () => container.removeEventListener('drop', onDrop),
    )
  }
}

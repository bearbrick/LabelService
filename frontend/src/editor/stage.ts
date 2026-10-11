/**
 * 设计画布：管理元素节点、选择框、框选、拖动吸附、变换。舞台、缩放平移和坐标换算在基类 StageViewport（stageViewport.ts）。
 * 模板数据由外部的 store 持有，这里只读数据、在拖动或变换结束时改坐标，再通知外部记录撤销历史。
 * 每个元素记一份“内容签名”，只有内容变了才重画，100 个元素时拖动也流畅。
 */
import Konva from 'konva'
import type { LabelElement } from '../model/template.ts'
import { normalizeRotation, roundMm } from '../model/units.ts'
import { drawElement, setGeometry } from './draw.ts'
import { boxFromPoints, contains, elementBounds } from './geometry.ts'
import type { Box } from './geometry.ts'
import { transformRules } from './overlays.ts'
import { snapBox } from './snapping.ts'
import { StageViewport } from './stageViewport.ts'
import type { StageHost } from './stageViewport.ts'

/**
 * 设计器的画布。
 */
export class DesignerStage extends StageViewport {
  /**
   * 变换框（缩放、旋转手柄）。
   */
  private readonly transformer: Konva.Transformer
  /**
   * 拖动吸附时显示的参考线。
   */
  private readonly guides = new Konva.Group({ listening: false })
  /**
   * 框选框。
   */
  private readonly marquee = new Konva.Rect({ visible: false, listening: false })
  /**
   * 拖字段到条码等元素上时的高亮框。
   */
  private readonly dropBox = new Konva.Rect({ visible: false, listening: false })
  /**
   * 元素 ID → 元素的组。
   */
  private readonly nodes = new Map<string, Konva.Group>()
  /**
   * 元素 ID → 上次绘制时的内容签名（元素 JSON + 上下文签名）。
   */
  private readonly signatures = new Map<string, string>()
  /**
   * 框选的起点和开始时的选择；没在框选时为 null。
   */
  private marqueeStart: { x: number; y: number; additive: boolean; base: string[] } | null = null
  /**
   * 当前变换框的模式：普通、横线（只拉宽度）、竖线（只拉高度）。
   */
  private transformMode: 'box' | 'hline' | 'vline' = 'box'
  /**
   * 正在直接编辑文本的元素 ID；没有时为 null。
   */
  private editingId: string | null = null

  /**
   * 建画布：在基类的舞台上加变换框和覆盖层，绑定选择、框选、双击事件。
   * @param wrap 画布的外层容器。
   * @param container Konva 的容器。
   * @param host 外部接口。
   */
  constructor(wrap: HTMLElement, container: HTMLDivElement, host: StageHost) {
    super(wrap, container, host)
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
      // 拉到太小（屏幕上不足 6 像素）时不再缩小；线只限制它有长度的方向。
      boundBoxFunc: (oldBox, newBox) => {
        if (this.transformMode !== 'vline' && Math.abs(newBox.width) < 6) return oldBox
        if (this.transformMode !== 'hline' && Math.abs(newBox.height) < 6) return oldBox
        return newBox
      },
    })
    this.overlay.add(this.guides, this.marquee, this.dropBox, this.transformer)
    this.applyColors()
    this.bindEvents()
  }

  /**
   * 按模板同步节点：新建、删除、重画内容变化的元素、调整叠放顺序。
   */
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
      // 内容签名没变就不重画，拖动时只改位置。
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

  /**
   * 按当前选择更新变换框：线只能拉长度，二维码和图标等比缩放，锁定的元素不能变换。
   */
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

  /**
   * 选中元素的屏幕外框，给标尺高亮用。
   * @returns 外框（px）；没有选中时为 null。
   */
  selectionScreenBox(): Box | null {
    return this.transformer.nodes().length ? this.transformer.getClientRect() : null
  }

  /**
   * 拖放事件下方的元素 ID。
   * @param event 拖放事件。
   * @returns 元素 ID；下方没有元素时为 null。
   */
  elementAt(event: DragEvent): string | null {
    this.stage.setPointersPositions(event)
    const position = this.stage.getPointerPosition()
    const shape = position ? this.main.getIntersection(position) : null
    return shape?.findAncestor('.element', true)?.id() ?? null
  }

  /**
   * 拖字段到条码上时高亮目标元素；传 null 取消。
   * @param box 目标元素的外框（mm）；null 表示取消高亮。
   */
  highlight(box: Box | null): void {
    this.dropBox.setAttrs(box
      ? { ...box, width: Math.max(box.width, 1), height: Math.max(box.height, 1), stroke: this.colors.accent, strokeWidth: 2 / this.scaleValue, fill: 'rgba(21,85,212,0.09)', visible: true }
      : { visible: false })
    this.overlay.batchDraw()
  }

  /**
   * 正在直接编辑文本的元素在画布上隐藏，由外部的输入框代替。
   * @param id 正在编辑的元素 ID；null 表示结束编辑。
   */
  setEditing(id: string | null): void {
    this.editingId = id
    this.sync()
  }

  /**
   * 视图变化后：框选框的线宽保持 1 像素，清掉参考线，再重画并通知外部。
   */
  protected override afterView(): void {
    this.marquee.strokeWidth(1 / this.scaleValue)
    this.guides.destroyChildren()
    super.afterView()
  }

  /**
   * 变换框用主题的强调色。
   */
  private applyColors(): void {
    this.transformer.setAttrs({ borderStroke: this.colors.accent, anchorStroke: this.colors.accent })
  }

  /**
   * 给新元素建一个组，绑定拖动、变换和光标样式。
   * @param element 元素。
   * @returns 组（已加到元素层）。
   */
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

  /**
   * 按 ID 找模板里的元素。
   * @param id 元素 ID。
   * @returns 元素；不存在时为 undefined。
   */
  private find(id: string): LabelElement | undefined {
    return this.host.template().elements.find((element) => element.id === id)
  }

  /**
   * 绑定舞台事件：按下（选择或开始框选）、移动（光标位置、框选框）、离开、双击；松开鼠标时结束框选。
   */
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

    // 框选可能在画布外松开鼠标，结束框选的监听挂在 window 上。
    const endMarquee = () => this.endMarquee()
    window.addEventListener('mouseup', endMarquee)
    window.addEventListener('touchend', endMarquee)
    this.cleanup.push(
      () => window.removeEventListener('mouseup', endMarquee),
      () => window.removeEventListener('touchend', endMarquee),
    )
  }

  /**
   * 按下鼠标：点元素选择它（Shift、Ctrl 加选或减选），点空白处开始框选。中键、右键、平移模式、变换框手柄不处理。
   * @param event Konva 的鼠标或触摸事件。
   */
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

  /**
   * 结束框选：选中完全在框里的可见元素；框太小（不足 3 像素）时当作点击空白处。
   */
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

  /**
   * 开始拖动：平移模式下取消；拖的元素没选中时只选中它。
   * @param group 被拖动的组。
   */
  private onDragStart(group: Konva.Group): void {
    if (this.host.settings().panMode) {
      group.stopDrag()
      return
    }

    if (!this.host.selection().includes(group.id())) this.host.select([group.id()])
  }

  /**
   * 拖动中吸附到参考线或网格；按住 Alt 暂时不吸附。
   * @param group 被拖动的组。
   * @param event 鼠标事件（看 Alt 键）。
   */
  private onDragMove(group: Konva.Group, event: MouseEvent): void {
    this.guides.destroyChildren()
    const settings = this.host.settings()
    const element = this.find(group.id())
    if (!element || !settings.snap || event?.altKey) {
      this.overlay.batchDraw()
      this.host.viewChanged()
      return
    }

    // 组的位置是元素中心；参考线来自其他没被选中的可见元素。
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

  /**
   * 选中的元素一起移动（变换框会带着其他选中节点），结束时统一写回坐标。
   * @param group 被拖动的组。
   */
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

  /**
   * 把缩放折算成宽高，保持节点 scale 为 1，元素内容按新尺寸重画。
   * @param group 正在变换的组。
   */
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

  /**
   * 变换结束：条码的旋转角度对齐到 90° 的整数倍（服务端只支持这几种），记录撤销历史。
   * @param group 变换完的组。
   */
  private onTransformEnd(group: Konva.Group): void {
    const element = this.find(group.id())
    if (element?.type === 'barcode' && (element.rotation ?? 0) % 90) {
      element.rotation = (Math.round((element.rotation ?? 0) / 90) * 90) % 360
      setGeometry(group, element)
      this.host.notify('条码只能旋转 0°、90°、180°、270°，已自动对齐')
    }

    this.host.committed()
  }
}

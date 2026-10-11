/**
 * 设计器的元素操作：新建、删除、复制粘贴、对齐分布、叠放顺序、锁定隐藏、微调、线的方向、旋转。
 * 每个操作改完模板后调 commit() 记录撤销历史（微调用 commitSoon() 合并连续的按键）。
 */
import { alignElements, distributeElements, moveAbove, reorder } from '../editor/arrange.ts'
import type { AlignKind, OrderKind } from '../editor/arrange.ts'
import { readClipboard, writeClipboard } from '../editor/clipboard.ts'
import { cloneElements, makeElement } from '../model/elements.ts'
import type { ElementType, LabelElement } from '../model/template.ts'
import { roundMm } from '../model/units.ts'
import { commit, commitSoon } from './designerHistory.ts'
import { allSelectedLocked, designer, selectedElements, toast, viewCenter } from './designerState.ts'

/**
 * 同一次复制已经粘贴了几次（每次多偏移 2 mm，粘贴的不会叠在一起）。
 */
let pasteCount = 0

/**
 * 新建元素：放在指定位置（拖入时）或视图中心，并限制在画布内。
 * @param type 元素类型。
 * @param extra 覆盖的属性。
 * @param at 放置的中心点，mm；不传时放在视图中心。
 * @returns 新元素（已选中）。
 */
export function addElement(type: ElementType, extra: Partial<LabelElement> = {}, at?: { x: number; y: number }): LabelElement {
  const template = designer.template
  const element = makeElement(type, template.fields, template.elements, extra)
  const center = at ?? viewCenter() ?? { x: template.page.width / 2, y: template.page.height / 2 }
  element.x = roundMm(Math.min(Math.max(center.x - element.width / 2, 0), Math.max(0, template.page.width - element.width)))
  element.y = roundMm(Math.min(Math.max(center.y - element.height / 2, 0), Math.max(0, template.page.height - element.height)))
  template.elements.push(element)
  designer.selection = [element.id]
  commit()
  return element
}

/**
 * 删除选中的元素；锁定的保留并提示。
 */
export function removeSelected(): void {
  const unlocked = selectedElements.value.filter((element) => !element.locked)
  if (!unlocked.length) {
    if (selectedElements.value.length) toast('选中的元素已锁定，先解锁再删除', true)
    return
  }

  const ids = new Set(unlocked.map((element) => element.id))
  const kept = selectedElements.value.length - unlocked.length
  designer.template.elements = designer.template.elements.filter((element) => !ids.has(element.id))
  designer.selection = designer.selection.filter((id) => !ids.has(id))
  commit()
  if (kept) toast(`已删除 ${ids.size} 个，${kept} 个锁定的元素保留`)
}

/**
 * 原地复制一份，偏移 2mm。
 */
export function duplicate(): void {
  if (!selectedElements.value.length) return
  const copies = cloneElements(selectedElements.value, 2, designer.template.elements)
  copies.forEach((copy) => (copy.name = `${copy.name} 副本`))
  designer.template.elements.push(...copies)
  designer.selection = copies.map((copy) => copy.id)
  commit()
}

/**
 * 复制选中的元素到剪贴板（换模板后也能粘贴）。
 */
export function copySelection(): void {
  if (!selectedElements.value.length) return
  writeClipboard(selectedElements.value)
  pasteCount = 0
  toast(`已复制 ${selectedElements.value.length} 个元素，可以粘贴到其他模板`)
}

/**
 * 粘贴剪贴板里的元素：换新 ID，每粘贴一次多偏移 2 mm，粘贴后选中它们。
 */
export function paste(): void {
  const elements = readClipboard()
  if (!elements) return
  pasteCount++
  const copies = cloneElements(elements, 2 * pasteCount, designer.template.elements)
  designer.template.elements.push(...copies)
  designer.selection = copies.map((copy) => copy.id)
  commit()
}

/**
 * 对齐选中的元素（只选一个时对齐到画布）。
 * @param kind 对齐方式。
 */
export function align(kind: AlignKind): void {
  alignElements(selectedElements.value, kind, designer.template.page)
  commit()
}

/**
 * 等间距分布选中的元素（至少 3 个）。
 * @param axis h 水平，v 垂直。
 */
export function distribute(axis: 'h' | 'v'): void {
  distributeElements(selectedElements.value, axis)
  commit()
}

/**
 * 调整选中元素的叠放顺序。
 * @param kind 置顶、置底、上移、下移。
 */
export function order(kind: OrderKind): void {
  if (!designer.selection.length) return
  designer.template.elements = reorder(designer.template.elements, new Set(designer.selection), kind)
  commit()
}

/**
 * 图层面板拖动排序：把一个元素放到另一个元素上面。
 * @param movingId 拖动的元素 ID。
 * @param targetId 放到哪个元素上面。
 */
export function moveLayer(movingId: string, targetId: string): void {
  designer.template.elements = moveAbove(designer.template.elements, movingId, targetId)
  commit()
}

/**
 * 锁定或解锁选中的元素（都锁定时解锁，否则全部锁定）。
 */
export function toggleLockSelection(): void {
  const lock = !allSelectedLocked.value
  selectedElements.value.forEach((element) => (element.locked = lock))
  commit()
  toast(lock ? '已锁定，不能拖动和缩放' : '已解锁')
}

/**
 * 锁定或解锁一个元素（图层面板的锁按钮）。
 * @param element 元素。
 */
export function toggleLock(element: LabelElement): void {
  element.locked = !element.locked
  commit()
}

/**
 * 隐藏或显示一个元素；隐藏时从选择里去掉。
 * @param element 元素。
 */
export function toggleHidden(element: LabelElement): void {
  element.hidden = !element.hidden
  if (element.hidden) designer.selection = designer.selection.filter((id) => id !== element.id)
  commit()
}

/**
 * 方向键微调：0.1mm，按住 Shift 1mm。
 * @param dx 横向移动，mm。
 * @param dy 纵向移动，mm。
 */
export function nudge(dx: number, dy: number): void {
  for (const element of selectedElements.value) {
    if (element.locked) continue
    element.x = roundMm(element.x + dx)
    element.y = roundMm(element.y + dy)
  }

  commitSoon()
}

/**
 * 把线改成横线或竖线（长度取原来较长的一边，至少 30 mm），旋转归零。
 * @param element 线元素。
 * @param direction h 横线，v 竖线。
 */
export function setLineDirection(element: LabelElement, direction: 'h' | 'v'): void {
  const length = Math.max(element.width, element.height) || 30
  element.width = direction === 'h' ? length : 0
  element.height = direction === 'h' ? 0 : length
  element.rotation = 0
  commit()
}

/**
 * 修改旋转角度；条码只允许 0、90、180、270。
 * @param element 元素。
 * @param degrees 角度。
 */
export function setRotation(element: LabelElement, degrees: number): void {
  let rotation = ((Number(degrees) || 0) % 360 + 360) % 360
  if (element.type === 'barcode' && rotation % 90) {
    rotation = (Math.round(rotation / 90) * 90) % 360
    toast('条码只能旋转 0°、90°、180°、270°', true)
  }

  element.rotation = roundMm(rotation)
  commit()
}

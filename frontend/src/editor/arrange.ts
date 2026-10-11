/**
 * 对齐、等间距分布、调整叠放顺序。直接修改传入的元素，坐标保留 1 位小数。锁定的元素参与计算但不移动。
 * 对齐和分布只用到位置和尺寸，拼版设计器的格子（按所占矩形）也用它们。
 */
import type { LabelElement } from '../model/template.ts'
import { roundMm } from '../model/units.ts'
import { unionBox } from './geometry.ts'
import type { Box } from './geometry.ts'

/**
 * 对齐方式：左、水平居中、右、上、垂直居中、下。
 */
export type AlignKind = 'left' | 'hcenter' | 'right' | 'top' | 'vcenter' | 'bottom'

/**
 * 叠放顺序操作：置于顶层、置于底层、上移一层、下移一层。
 */
export type OrderKind = 'front' | 'back' | 'up' | 'down'

/**
 * 能对齐、分布的对象：模板元素，或拼版设计器里格子所占的矩形。
 */
export interface Movable {
  /**
   * 左上角 x，mm。
   */
  x: number
  /**
   * 左上角 y，mm。
   */
  y: number
  /**
   * 宽度，mm。
   */
  width: number
  /**
   * 高度，mm。
   */
  height: number
  /**
   * 锁定的不移动。
   */
  locked?: boolean
}

/**
 * 取对象的矩形。
 * @param element 对象。
 * @returns 矩形。
 */
const boxOf = (element: Movable): Box => ({ x: element.x, y: element.y, width: element.width, height: element.height })

/**
 * 对齐。只选一个元素时对齐到画布，多个时对齐到选区外框。
 * @param selected 选中的对象（直接修改）。
 * @param kind 对齐方式。
 * @param page 画布尺寸（只选一个时对齐到它）。
 */
export function alignElements(selected: readonly Movable[], kind: AlignKind, page: { width: number; height: number }): void {
  if (!selected.length) return
  const reference = selected.length === 1 ? { x: 0, y: 0, width: page.width, height: page.height } : unionBox(selected.map(boxOf))!
  for (const element of selected) {
    if (element.locked) continue
    switch (kind) {
      case 'left':
        element.x = reference.x
        break
      case 'hcenter':
        element.x = reference.x + reference.width / 2 - element.width / 2
        break
      case 'right':
        element.x = reference.x + reference.width - element.width
        break
      case 'top':
        element.y = reference.y
        break
      case 'vcenter':
        element.y = reference.y + reference.height / 2 - element.height / 2
        break
      case 'bottom':
        element.y = reference.y + reference.height - element.height
        break
    }

    element.x = roundMm(element.x)
    element.y = roundMm(element.y)
  }
}

/**
 * 等间距分布，至少 3 个元素。首尾两个不动，中间的按相等间隙排开。
 * @param selected 选中的对象（直接修改）。
 * @param axis h 水平分布，v 垂直分布。
 */
export function distributeElements(selected: readonly Movable[], axis: 'h' | 'v'): void {
  if (selected.length < 3) return
  const position = axis === 'h' ? 'x' : 'y'
  const size = axis === 'h' ? 'width' : 'height'
  const sorted = [...selected].sort((a, b) => a[position] - b[position])
  const first = sorted[0]
  const last = sorted[sorted.length - 1]
  const span = last[position] + last[size] - first[position]
  const total = sorted.reduce((sum, element) => sum + element[size], 0)
  const gap = (span - total) / (sorted.length - 1)
  let cursor = first[position]
  for (const element of sorted) {
    if (!element.locked) element[position] = roundMm(cursor)
    cursor += element[size] + gap
  }
}

/**
 * 调整叠放顺序，返回新数组（数组后面的在上层）。
 * @param elements 全部元素。
 * @param ids 要调整的元素 ID。
 * @param kind 操作。
 * @returns 调整后的新数组。
 */
export function reorder(elements: readonly LabelElement[], ids: ReadonlySet<string>, kind: OrderKind): LabelElement[] {
  const list = [...elements]
  switch (kind) {
    case 'front':
      return [...list.filter((element) => !ids.has(element.id)), ...list.filter((element) => ids.has(element.id))]
    case 'back':
      return [...list.filter((element) => ids.has(element.id)), ...list.filter((element) => !ids.has(element.id))]
    case 'up':
      for (let i = list.length - 2; i >= 0; i--) {
        if (ids.has(list[i].id) && !ids.has(list[i + 1].id)) [list[i], list[i + 1]] = [list[i + 1], list[i]]
      }

      return list
    case 'down':
      for (let i = 1; i < list.length; i++) {
        if (ids.has(list[i].id) && !ids.has(list[i - 1].id)) [list[i], list[i - 1]] = [list[i - 1], list[i]]
      }

      return list
  }
}

/**
 * 图层面板拖动：把元素放到目标元素的上一层（数组中紧随其后），返回新数组。
 * @param elements 全部元素。
 * @param movingId 拖动的元素 ID。
 * @param targetId 放到哪个元素上面；找不到时放到最上层。
 * @returns 调整后的新数组。
 */
export function moveAbove(elements: readonly LabelElement[], movingId: string, targetId: string): LabelElement[] {
  if (movingId === targetId) return [...elements]
  const moving = elements.find((element) => element.id === movingId)
  if (!moving) return [...elements]
  const rest = elements.filter((element) => element.id !== movingId)
  const index = rest.findIndex((element) => element.id === targetId)
  rest.splice(index < 0 ? rest.length : index + 1, 0, moving)
  return rest
}

/**
 * 对齐、等间距分布、调整叠放顺序。直接修改传入的元素，坐标保留 1 位小数。锁定的元素参与计算但不移动。
 */
import type { LabelElement } from '../model/template.ts'
import { roundMm } from '../model/units.ts'
import { unionBox } from './geometry.ts'
import type { Box } from './geometry.ts'

export type AlignKind = 'left' | 'hcenter' | 'right' | 'top' | 'vcenter' | 'bottom'
export type OrderKind = 'front' | 'back' | 'up' | 'down'

const boxOf = (element: LabelElement): Box => ({ x: element.x, y: element.y, width: element.width, height: element.height })

/** 对齐。只选一个元素时对齐到画布，多个时对齐到选区外框。 */
export function alignElements(selected: readonly LabelElement[], kind: AlignKind, page: { width: number; height: number }): void {
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

/** 等间距分布，至少 3 个元素。首尾两个不动，中间的按相等间隙排开。 */
export function distributeElements(selected: readonly LabelElement[], axis: 'h' | 'v'): void {
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

/** 调整叠放顺序，返回新数组（数组后面的在上层）。 */
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

/** 图层面板拖动：把元素放到目标元素的上一层（数组中紧随其后），返回新数组。 */
export function moveAbove(elements: readonly LabelElement[], movingId: string, targetId: string): LabelElement[] {
  if (movingId === targetId) return [...elements]
  const moving = elements.find((element) => element.id === movingId)
  if (!moving) return [...elements]
  const rest = elements.filter((element) => element.id !== movingId)
  const index = rest.findIndex((element) => element.id === targetId)
  rest.splice(index < 0 ? rest.length : index + 1, 0, moving)
  return rest
}

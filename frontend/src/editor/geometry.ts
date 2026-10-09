/**
 * 元素的几何计算，单位 mm。旋转绕元素中心、顺时针（和服务端一致）。
 */
import type { LabelElement } from '../model/template.ts'

export interface Box {
  x: number
  y: number
  width: number
  height: number
}

/** 元素旋转后的外接矩形。 */
export function elementBounds(element: Pick<LabelElement, 'x' | 'y' | 'width' | 'height' | 'rotation'>): Box {
  const rotation = ((element.rotation ?? 0) % 360 + 360) % 360
  if (rotation === 0) return { x: element.x, y: element.y, width: element.width, height: element.height }
  const cx = element.x + element.width / 2
  const cy = element.y + element.height / 2
  const radians = (rotation * Math.PI) / 180
  const cos = Math.cos(radians)
  const sin = Math.sin(radians)
  const corners = [
    [-element.width / 2, -element.height / 2],
    [element.width / 2, -element.height / 2],
    [element.width / 2, element.height / 2],
    [-element.width / 2, element.height / 2],
  ].map(([dx, dy]) => [cx + dx * cos - dy * sin, cy + dx * sin + dy * cos])
  const xs = corners.map((corner) => corner[0])
  const ys = corners.map((corner) => corner[1])
  const x = Math.min(...xs)
  const y = Math.min(...ys)
  return { x, y, width: Math.max(...xs) - x, height: Math.max(...ys) - y }
}

/** 多个矩形的并集外框；空数组返回 null。 */
export function unionBox(boxes: readonly Box[]): Box | null {
  if (!boxes.length) return null
  const x = Math.min(...boxes.map((box) => box.x))
  const y = Math.min(...boxes.map((box) => box.y))
  const right = Math.max(...boxes.map((box) => box.x + box.width))
  const bottom = Math.max(...boxes.map((box) => box.y + box.height))
  return { x, y, width: right - x, height: bottom - y }
}

/** 两个矩形是否相交。宽或高为 0 的线条按 0.01mm 算，框选也能选中。 */
export function intersects(a: Box, b: Box): boolean {
  const aw = Math.max(a.width, 0.01)
  const ah = Math.max(a.height, 0.01)
  const bw = Math.max(b.width, 0.01)
  const bh = Math.max(b.height, 0.01)
  return a.x < b.x + bw && a.x + aw > b.x && a.y < b.y + bh && a.y + ah > b.y
}

/** 外框 outer 是否完全包住 inner（框选用：和 PowerPoint 一样，只选完全在框里的元素）。 */
export function contains(outer: Box, inner: Box): boolean {
  const epsilon = 1e-6
  return inner.x >= outer.x - epsilon && inner.y >= outer.y - epsilon && inner.x + inner.width <= outer.x + outer.width + epsilon && inner.y + inner.height <= outer.y + outer.height + epsilon
}

/** 由两个对角点得到矩形。 */
export function boxFromPoints(x1: number, y1: number, x2: number, y2: number): Box {
  return { x: Math.min(x1, x2), y: Math.min(y1, y2), width: Math.abs(x2 - x1), height: Math.abs(y2 - y1) }
}

/**
 * 拖动时的吸附：先找对齐参考线（其他元素和画布的边、中线），找不到再吸附到网格。
 */
import type { Box } from './geometry.ts'

/**
 * 吸附的设置。
 */
export interface SnapOptions {
  /**
   * 吸附距离（mm），一般是屏幕上 5 像素换算出来的值。
   */
  threshold: number
  /**
   * 网格间距（mm）；null 表示不吸附网格。
   */
  grid: number | null
}

/**
 * 吸附的结果。
 */
export interface SnapResult {
  /**
   * 横向需要再移动的距离，mm。
   */
  dx: number
  /**
   * 纵向需要再移动的距离，mm。
   */
  dy: number
  /**
   * 命中的竖向参考线 x（mm），没有为 null。
   */
  guideX: number | null
  /**
   * 命中的横向参考线 y（mm），没有为 null。
   */
  guideY: number | null
}

/**
 * 计算拖动中的元素应该吸附到哪里。
 * @param box 拖动中的元素外框。
 * @param others 其他元素的外框（参考线来源）。
 * @param page 画布尺寸（边和中线也是参考线）。
 * @param options 吸附设置。
 * @returns 再移动的距离和命中的参考线。
 */
export function snapBox(box: Box, others: readonly Box[], page: { width: number; height: number }, options: SnapOptions): SnapResult {
  const verticals = [0, page.width / 2, page.width]
  const horizontals = [0, page.height / 2, page.height]
  for (const other of others) {
    verticals.push(other.x, other.x + other.width / 2, other.x + other.width)
    horizontals.push(other.y, other.y + other.height / 2, other.y + other.height)
  }

  const x = nearest(verticals, [box.x, box.x + box.width / 2, box.x + box.width], options.threshold)
  const y = nearest(horizontals, [box.y, box.y + box.height / 2, box.y + box.height], options.threshold)
  return {
    dx: x ? x.delta : gridDelta(box.x, options.grid),
    dy: y ? y.delta : gridDelta(box.y, options.grid),
    guideX: x ? x.line : null,
    guideY: y ? y.line : null,
  }
}

/**
 * 在参考线里找离元素某条边最近、且在吸附距离以内的一条。
 * @param lines 参考线位置。
 * @param edges 元素的边（左/中/右或上/中/下）。
 * @param threshold 吸附距离。
 * @returns 要移动的距离和参考线；没有在距离以内的时为 null。
 */
function nearest(lines: readonly number[], edges: readonly number[], threshold: number): { delta: number; line: number } | null {
  let best: { delta: number; line: number } | null = null
  for (const line of lines) {
    for (const edge of edges) {
      const delta = line - edge
      if (Math.abs(delta) < threshold && (!best || Math.abs(delta) < Math.abs(best.delta))) best = { delta, line }
    }
  }

  return best
}

/**
 * 吸附到网格要移动的距离。
 * @param value 当前坐标。
 * @param grid 网格间距；null 表示不吸附网格。
 * @returns 要移动的距离（不吸附时为 0）。
 */
function gridDelta(value: number, grid: number | null): number {
  if (!grid) return 0
  return Math.round(value / grid) * grid - value
}

/**
 * 拖动时的吸附：先找对齐参考线（其他元素和画布的边、中线），找不到再吸附到网格。
 */
import type { Box } from './geometry.ts'

export interface SnapOptions {
  /** 吸附距离（mm），一般是屏幕上 5 像素换算出来的值。 */
  threshold: number
  /** 网格间距（mm）；null 表示不吸附网格。 */
  grid: number | null
}

export interface SnapResult {
  /** 需要再移动的距离（mm）。 */
  dx: number
  dy: number
  /** 命中的竖向参考线 x（mm），没有为 null。 */
  guideX: number | null
  /** 命中的横向参考线 y（mm），没有为 null。 */
  guideY: number | null
}

/** 计算拖动中的元素应该吸附到哪里。 */
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

function gridDelta(value: number, grid: number | null): number {
  if (!grid) return 0
  return Math.round(value / grid) * grid - value
}

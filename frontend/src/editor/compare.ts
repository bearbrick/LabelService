/**
 * 设计器画布和服务端 PNG 的位置对比（验收标准：元素位置偏差 ≤ 0.2 mm）。
 * 两张图按同样的 DPI、白底，逐个元素在它的外框（向外多留一点）里找“墨迹”的上下左右边界，比较两边边界的差。
 * 只做像素计算，不碰 DOM；导出画布和解码 PNG 在 PreviewDialog 里做。
 */
import { elementBounds } from './geometry.ts'
import type { LabelElement } from '../model/template.ts'

/**
 * RGBA 像素，和 ImageData 一样的布局。
 */
export interface Pixels {
  /**
   * 像素数据，每个像素 RGBA 四个字节。
   */
  data: Uint8ClampedArray
  /**
   * 宽度，像素。
   */
  width: number
  /**
   * 高度，像素。
   */
  height: number
}

/**
 * 矩形的四条边，像素（含边界）。
 */
export interface Edges {
  /**
   * 左边。
   */
  left: number
  /**
   * 上边。
   */
  top: number
  /**
   * 右边。
   */
  right: number
  /**
   * 下边。
   */
  bottom: number
}

/**
 * 一个元素的位置偏差。
 */
export interface ElementOffset {
  /**
   * 元素 ID。
   */
  id: string
  /**
   * 元素名称（没有名称时是 ID）。
   */
  name: string
  /**
   * 四条边里最大的偏差，mm。两边都没有墨迹时为 0；只有一边有时为 Infinity。
   */
  maxMm: number
}

/**
 * 区域（像素，含边界）里比阈值暗的像素的外框；没有时返回 null。
 * @param pixels 图片。
 * @param region 查找的区域，像素。
 * @param threshold 亮度阈值，低于它算墨迹，默认 160。
 * @returns 墨迹的外框，或 null。
 */
export function inkEdges(pixels: Pixels, region: Edges, threshold = 160): Edges | null {
  const left = Math.max(0, Math.floor(region.left))
  const top = Math.max(0, Math.floor(region.top))
  const right = Math.min(pixels.width - 1, Math.ceil(region.right))
  const bottom = Math.min(pixels.height - 1, Math.ceil(region.bottom))
  let found: Edges | null = null
  for (let y = top; y <= bottom; y++) {
    for (let x = left; x <= right; x++) {
      const i = (y * pixels.width + x) * 4
      const luminance = 0.299 * pixels.data[i] + 0.587 * pixels.data[i + 1] + 0.114 * pixels.data[i + 2]
      if (luminance >= threshold) continue
      if (!found) found = { left: x, top: y, right: x, bottom: y }
      else {
        found.left = Math.min(found.left, x)
        found.right = Math.max(found.right, x)
        found.top = Math.min(found.top, y)
        found.bottom = Math.max(found.bottom, y)
      }
    }
  }

  return found
}

/**
 * 逐个可见元素比较两张图（同尺寸、白底）。pxPerMm 是图上每 mm 的像素数；margin 是外框向外多看的距离，mm。
 * 结果按偏差从大到小排列。
 * @param elements 模板的元素。
 * @param canvas 设计器画布导出的图片。
 * @param server 服务端预览的图片。
 * @param pxPerMm 图上每 mm 的像素数。
 * @param margin 外框向外多看的距离，mm。
 * @returns 每个可见元素的偏差，大的在前。
 */
export function compareElements(elements: readonly LabelElement[], canvas: Pixels, server: Pixels, pxPerMm: number, margin = 1): ElementOffset[] {
  const result: ElementOffset[] = []
  for (const element of elements) {
    if (element.hidden) continue
    const box = elementBounds(element)
    const region = {
      left: (box.x - margin) * pxPerMm,
      top: (box.y - margin) * pxPerMm,
      right: (box.x + box.width + margin) * pxPerMm,
      bottom: (box.y + box.height + margin) * pxPerMm,
    }
    const a = inkEdges(canvas, region)
    const b = inkEdges(server, region)
    let maxMm = 0
    if (a && b) maxMm = Math.max(Math.abs(a.left - b.left), Math.abs(a.top - b.top), Math.abs(a.right - b.right), Math.abs(a.bottom - b.bottom)) / pxPerMm
    else if (a || b) maxMm = Infinity
    result.push({ id: element.id, name: element.name || element.id, maxMm })
  }

  return result.sort((x, y) => y.maxMm - x.maxMm)
}

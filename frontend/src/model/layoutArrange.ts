/**
 * 拼版设计器的排列操作：自动排列、按纸张算能排几行几列、重新编号、调整填充顺序、新建版式、默认编码。
 * 都是纯函数（不改传入的对象），Node 能直接运行，有检查（tests/checks.ts）。
 */
import { footprint, TOLERANCE } from './layout.ts'
import type { FillOrder, SheetLayout, SheetSize, SheetSlot } from './layout.ts'
import { roundMm } from './units.ts'

/**
 * 自动排列的参数。
 */
export interface ArrangeOptions {
  /**
   * 行数。
   */
  rows: number
  /**
   * 列数。
   */
  cols: number
  /**
   * 上边距，mm。
   */
  marginTop: number
  /**
   * 左边距，mm。
   */
  marginLeft: number
  /**
   * 列间距，mm。
   */
  gapX: number
  /**
   * 行间距，mm。
   */
  gapY: number
  /**
   * 格子都转 90°（竖着放）。
   */
  rotate: boolean
  /**
   * 填充顺序。
   */
  order: FillOrder
}

/**
 * 自动排列：按行列、页边距、间距排满，坐标保留 1 位小数。
 * @param slotSize 格子尺寸。
 * @param options 排列参数。
 * @returns 格子，按填充顺序。
 */
export function arrange(slotSize: SheetSize, options: ArrangeOptions): SheetSlot[] {
  const rotation = options.rotate ? 90 : 0
  const size = footprint(slotSize, { x: 0, y: 0, rotation })
  const slots: SheetSlot[] = []
  const rows = Math.max(0, Math.floor(options.rows))
  const cols = Math.max(0, Math.floor(options.cols))
  // 外层循环决定填充顺序：先行后列时外层是行，先列后行时外层是列。
  for (let outer = 0; outer < (options.order === 'rows' ? rows : cols); outer++) {
    for (let inner = 0; inner < (options.order === 'rows' ? cols : rows); inner++) {
      const row = options.order === 'rows' ? outer : inner
      const col = options.order === 'rows' ? inner : outer
      slots.push({
        x: roundMm(options.marginLeft + col * (size.width + options.gapX)),
        y: roundMm(options.marginTop + row * (size.height + options.gapY)),
        rotation,
      })
    }
  }

  return slots
}

/**
 * 纸上最多能排几行几列，页边距让整体居中（向下取到 0.1 mm）。
 * @param paper 纸张尺寸。
 * @param slotSize 格子尺寸。
 * @param rotate 格子都转 90°。
 * @param gapX 列间距，mm。
 * @param gapY 行间距，mm。
 * @returns 行数、列数、上边距、左边距。
 */
export function fitGrid(paper: SheetSize, slotSize: SheetSize, rotate: boolean, gapX = 0, gapY = 0): Pick<ArrangeOptions, 'rows' | 'cols' | 'marginTop' | 'marginLeft'> {
  const size = footprint(slotSize, { x: 0, y: 0, rotation: rotate ? 90 : 0 })
  const cols = Math.max(0, Math.floor((paper.width + gapX + TOLERANCE) / (size.width + gapX)))
  const rows = Math.max(0, Math.floor((paper.height + gapY + TOLERANCE) / (size.height + gapY)))
  const margin = (total: number, count: number, length: number, gap: number) => Math.max(0, Math.floor(((total - count * length - Math.max(0, count - 1) * gap) / 2) * 10) / 10)
  return { rows, cols, marginTop: margin(paper.height, rows, size.height, gapY), marginLeft: margin(paper.width, cols, size.width, gapX) }
}

/**
 * 按位置重新编号：先行后列（一行一行从左到右）或先列后行（一列一列从上到下）。
 * 同一行（列）的判断：格子左上角和这一行第一个格子相差不到 1 mm。
 * @param slots 格子。
 * @param order 填充顺序。
 * @returns 重新排好顺序的格子（新对象）。
 */
export function renumber(slots: readonly SheetSlot[], order: FillOrder): SheetSlot[] {
  const primary = (slot: SheetSlot) => (order === 'rows' ? slot.y : slot.x)
  const secondary = (slot: SheetSlot) => (order === 'rows' ? slot.x : slot.y)
  const sorted = [...slots].sort((a, b) => primary(a) - primary(b) || secondary(a) - secondary(b))
  // 按主方向切成一条条“带”（行或列），每条带内再按次方向排序。
  const result: SheetSlot[] = []
  let band: SheetSlot[] = []
  for (const slot of sorted) {
    if (band.length && primary(slot) - primary(band[0]) >= 1) {
      result.push(...band.sort((a, b) => secondary(a) - secondary(b)))
      band = []
    }

    band.push(slot)
  }

  result.push(...band.sort((a, b) => secondary(a) - secondary(b)))
  return result.map((slot) => ({ ...slot }))
}

/**
 * 手动调整填充顺序：把第 from 个格子移到第 to 个（下标从 0 开始）。
 * @param slots 格子。
 * @param from 要移动的格子下标。
 * @param to 移到的位置。
 * @returns 调整后的格子（新对象）；from 越界时原样复制。
 */
export function moveSlot(slots: readonly SheetSlot[], from: number, to: number): SheetSlot[] {
  const list = slots.map((slot) => ({ ...slot }))
  if (from < 0 || from >= list.length) return list
  const [moving] = list.splice(from, 1)
  list.splice(Math.max(0, Math.min(list.length, to)), 0, moving)
  return list
}

/**
 * 新建版式：按纸张和格子尺寸排满（不旋转、无间距、整体居中、先行后列）。
 * @param code 版式编码。
 * @param name 名称。
 * @param paper 纸张尺寸。
 * @param slot 格子尺寸（所选模板的尺寸）。
 * @returns 新版式。
 */
export function newLayout(code: string, name: string, paper: SheetSize, slot: SheetSize): SheetLayout {
  const grid = fitGrid(paper, slot, false)
  return {
    code,
    name,
    paper: { ...paper },
    slot: { ...slot },
    slots: arrange(slot, { ...grid, gapX: 0, gapY: 0, rotate: false, order: 'rows' }),
    offset: { x: 0, y: 0 },
    cutLines: false,
  }
}

/**
 * 默认的版式编码：A4_100x60_2x4 这样（纸张_格子宽x高_列x行）。
 * @param paperName 纸张名称，如“A4 纵向”；不是 A 系列时写 SHEET。
 * @param slot 格子尺寸。
 * @param rows 行数。
 * @param cols 列数。
 * @returns 版式编码（小数点换成下划线）。
 */
export function suggestCode(paperName: string, slot: SheetSize, rows: number, cols: number): string {
  const paper = /^A\d/.test(paperName) ? paperName.slice(0, 2) : 'SHEET'
  return `${paper}_${slot.width}x${slot.height}_${cols}x${rows}`.replace(/\./g, '_')
}

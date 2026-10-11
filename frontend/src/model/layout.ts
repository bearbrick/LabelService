/**
 * 拼版版式（ADR 0005）：版式 JSON 的 TypeScript 镜像，和 backend/src/LabelService.Contracts/Layouts/SheetLayout.cs 一致，
 * 规范见 docs/contracts.md“拼版”。另有格子所占的矩形、越界和重叠检查、页数、取整；自动排列和重新编号在 layoutArrange.ts。
 * 长度一律 mm，原点在纸张左上角；slots 的顺序就是填充顺序。
 */
import { roundMm } from './units.ts'

/**
 * 宽高，mm。
 */
export interface SheetSize {
  /**
   * 宽度，mm。
   */
  width: number
  /**
   * 高度，mm。
   */
  height: number
}

/**
 * 版式里放一张标签的位置。
 */
export interface SheetSlot {
  /**
   * 格子在纸上所占矩形的左上角 x，mm。
   */
  x: number
  /**
   * 格子在纸上所占矩形的左上角 y，mm。
   */
  y: number
  /**
   * 标签顺时针旋转的角度：0、90、180、270；90、270 时所占矩形的宽高互换。
   */
  rotation: number
}

/**
 * 打印偏移，mm。
 */
export interface SheetOffset {
  /**
   * 向右为正。
   */
  x: number
  /**
   * 向下为正。
   */
  y: number
}

/**
 * 拼版版式：把多张标签排在一张纸上打印，和模板分开保存。
 */
export interface SheetLayout {
  /**
   * 版式编码，开放接口的 layout 参数。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 纸张尺寸。
   */
  paper: SheetSize
  /**
   * 格子尺寸，等于能用这个版式的模板尺寸。
   */
  slot: SheetSize
  /**
   * 格子，按填充顺序。
   */
  slots: SheetSlot[]
  /**
   * 打印偏移，作用于整页，向右、向下为正。
   */
  offset: SheetOffset
  /**
   * 是否画格子边框（裁切线）。
   */
  cutLines: boolean
}

/**
 * 管理接口列表里的一项：版式 JSON 加上启用状态和修改记录。
 */
export interface LayoutView {
  /**
   * 版式 JSON。
   */
  layout: SheetLayout
  /**
   * 是否启用；停用后开放接口返回 LAYOUT_NOT_FOUND。
   */
  enabled: boolean
  /**
   * 最后修改人。
   */
  updatedBy: string
  /**
   * 最后修改时间。
   */
  updatedAt: string
}

/**
 * 纸上的一个矩形，mm。
 */
export interface Box {
  /**
   * 左上角 x。
   */
  x: number
  /**
   * 左上角 y。
   */
  y: number
  /**
   * 宽度。
   */
  width: number
  /**
   * 高度。
   */
  height: number
}

/**
 * 填充顺序：先行后列（一行一行从左到右）或先列后行（一列一列从上到下）。
 */
export type FillOrder = 'rows' | 'cols'

/**
 * 格子允许的旋转角度。
 */
export const ROTATIONS = [0, 90, 180, 270]

/**
 * 一个版式最多几个格子（和服务端一致）。
 */
export const MAX_SLOTS = 500

/**
 * 纸张边长上限，mm。
 */
export const MAX_PAPER = 1000

/**
 * 贴边允许的误差（mm），和服务端一致：两边都按 0.1 mm 记录，浮点误差不算重叠或越界。
 */
export const TOLERANCE = 0.05

/**
 * 版式编码的规则：字母开头，只有字母、数字、_、-，最长 64 个字符。
 */
export const CODE_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/

/**
 * 新建版式时可选的纸张。
 */
export const PAPERS: ReadonlyArray<{ name: string; width: number; height: number }> = [
  { name: 'A4 纵向', width: 210, height: 297 },
  { name: 'A4 横向', width: 297, height: 210 },
  { name: 'A5 纵向', width: 148, height: 210 },
  { name: 'A5 横向', width: 210, height: 148 },
]

/**
 * 格子在纸上占的宽高。
 * @param slotSize 格子尺寸（即模板尺寸）。
 * @param slot 格子。
 * @returns 所占的宽高；转 90°、270° 时宽高互换。
 */
export function footprint(slotSize: SheetSize, slot: SheetSlot): SheetSize {
  return slot.rotation === 90 || slot.rotation === 270 ? { width: slotSize.height, height: slotSize.width } : { width: slotSize.width, height: slotSize.height }
}

/**
 * 格子在纸上占的矩形。
 * @param slotSize 格子尺寸。
 * @param slot 格子。
 * @returns 所占的矩形。
 */
export function slotBox(slotSize: SheetSize, slot: SheetSlot): Box {
  return { x: slot.x, y: slot.y, ...footprint(slotSize, slot) }
}

/**
 * 版式检查的结果。
 */
export interface LayoutProblems {
  /**
   * 全部问题，格子的问题带序号（从 1 开始，就是填充顺序）。和服务端保存前的检查一致。
   */
  messages: string[]
  /**
   * 越界或重叠的格子下标（从 0 开始），画布上标红。
   */
  badSlots: Set<number>
}

/**
 * 检查版式：编码、名称、纸张和格子尺寸、格子数、打印偏移、旋转角度、越界、重叠。
 * @param layout 版式。
 * @returns 问题；没有问题时 messages 为空。
 */
export function checkLayout(layout: SheetLayout): LayoutProblems {
  const messages: string[] = []
  const badSlots = new Set<number>()
  if (!CODE_PATTERN.test(layout.code)) messages.push('版式编码要以字母开头，只能有字母、数字、_、-，最长 64 个字符')
  if (!layout.name.trim() || layout.name.length > 128) messages.push('名称不能为空，最长 128 字')
  if (!inRange(layout.paper.width) || !inRange(layout.paper.height)) messages.push(`纸张宽高要在 0–${MAX_PAPER} mm 之间`)
  if (!inRange(layout.slot.width) || !inRange(layout.slot.height)) messages.push('格子宽高要大于 0')
  if (layout.slots.length === 0 || layout.slots.length > MAX_SLOTS) messages.push(`格子数要在 1–${MAX_SLOTS} 之间`)
  if (Math.abs(layout.offset.x) > 50 || Math.abs(layout.offset.y) > 50) messages.push('打印偏移要在 ±50 mm 以内')

  // 逐个格子检查，并和前面已经检查过的格子比较是否重叠（两边都标红）。
  const boxes: Box[] = []
  layout.slots.forEach((slot, index) => {
    const name = `第 ${index + 1} 个格子`
    if (!ROTATIONS.includes(slot.rotation)) {
      messages.push(`${name}的旋转只能是 0、90、180、270`)
      badSlots.add(index)
      boxes.push({ x: slot.x, y: slot.y, width: 0, height: 0 })
      return
    }

    const box = slotBox(layout.slot, slot)
    if (box.x < -TOLERANCE || box.y < -TOLERANCE || box.x + box.width > layout.paper.width + TOLERANCE || box.y + box.height > layout.paper.height + TOLERANCE) {
      messages.push(`${name}超出了纸张`)
      badSlots.add(index)
    }

    boxes.forEach((other, otherIndex) => {
      if (overlaps(box, other)) {
        messages.push(`${name}和第 ${otherIndex + 1} 个格子重叠`)
        badSlots.add(index)
        badSlots.add(otherIndex)
      }
    })
    boxes.push(box)
  })
  return { messages, badSlots }
}

/**
 * 两个矩形是否重叠（贴边在误差以内不算）。
 * @param a 矩形。
 * @param b 另一个矩形。
 * @returns 重叠时为 true。
 */
export function overlaps(a: Box, b: Box): boolean {
  return a.x < b.x + b.width - TOLERANCE && b.x < a.x + a.width - TOLERANCE && a.y < b.y + b.height - TOLERANCE && b.y < a.y + a.height - TOLERANCE
}

/**
 * 页数：第一页从第 startSlot 格开始，一页排满换下一页。和服务端一致。
 * @param slotCount 版式的格子数。
 * @param labels 标签张数。
 * @param startSlot 第一页从第几格开始，从 1 开始。
 * @returns 页数；没有格子或没有标签时为 0。
 */
export function pageCount(slotCount: number, labels: number, startSlot = 1): number {
  if (slotCount <= 0 || labels <= 0) return 0
  return Math.ceil((startSlot - 1 + labels) / slotCount)
}

/**
 * 长度按 0.1 mm 取整，返回新对象。
 * @param layout 版式。
 * @returns 取整后的新版式（编码、名称去掉首尾空白）。
 */
export function normalizeLayout(layout: SheetLayout): SheetLayout {
  return {
    code: layout.code.trim(),
    name: layout.name.trim(),
    paper: { width: roundMm(layout.paper.width), height: roundMm(layout.paper.height) },
    slot: { width: roundMm(layout.slot.width), height: roundMm(layout.slot.height) },
    slots: layout.slots.map((slot) => ({ x: roundMm(slot.x), y: roundMm(slot.y), rotation: slot.rotation })),
    offset: { x: roundMm(layout.offset.x), y: roundMm(layout.offset.y) },
    cutLines: !!layout.cutLines,
  }
}

/**
 * 长度是否在 0（不含）到纸张上限之间。
 * @param value 长度，mm。
 * @returns 在范围内时为 true。
 */
function inRange(value: number): boolean {
  return Number.isFinite(value) && value > 0 && value <= MAX_PAPER
}

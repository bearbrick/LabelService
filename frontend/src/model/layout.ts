/**
 * 拼版版式（ADR 0005）：版式 JSON 的 TypeScript 镜像，和 backend/src/LabelService.Contracts/Layouts/SheetLayout.cs 一致，
 * 规范见 docs/contracts.md“拼版”。另有拼版设计器用的纯函数：格子占的矩形、越界和重叠检查、自动排列、重新编号、页数。
 * 长度一律 mm，原点在纸张左上角；slots 的顺序就是填充顺序。
 */
import { roundMm } from './units.ts'

export interface SheetSize {
  width: number
  height: number
}

export interface SheetSlot {
  /** 格子在纸上所占矩形的左上角。 */
  x: number
  y: number
  /** 标签顺时针旋转的角度：0、90、180、270；90、270 时所占矩形的宽高互换。 */
  rotation: number
}

export interface SheetOffset {
  x: number
  y: number
}

export interface SheetLayout {
  code: string
  name: string
  paper: SheetSize
  /** 格子尺寸，等于能用这个版式的模板尺寸。 */
  slot: SheetSize
  slots: SheetSlot[]
  /** 打印偏移，作用于整页，向右、向下为正。 */
  offset: SheetOffset
  /** 是否画格子边框（裁切线）。 */
  cutLines: boolean
}

/** 管理接口列表里的一项：版式 JSON 加上启用状态和修改记录。 */
export interface LayoutView {
  layout: SheetLayout
  enabled: boolean
  updatedBy: string
  updatedAt: string
}

export interface Box {
  x: number
  y: number
  width: number
  height: number
}

export type FillOrder = 'rows' | 'cols'

export const ROTATIONS = [0, 90, 180, 270]
export const MAX_SLOTS = 500
export const MAX_PAPER = 1000
/** 贴边允许的误差（mm），和服务端一致：两边都按 0.1 mm 记录，浮点误差不算重叠或越界。 */
export const TOLERANCE = 0.05
export const CODE_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/

export const PAPERS: ReadonlyArray<{ name: string; width: number; height: number }> = [
  { name: 'A4 纵向', width: 210, height: 297 },
  { name: 'A4 横向', width: 297, height: 210 },
  { name: 'A5 纵向', width: 148, height: 210 },
  { name: 'A5 横向', width: 210, height: 148 },
]

/** 格子在纸上占的宽高。 */
export function footprint(slotSize: SheetSize, slot: SheetSlot): SheetSize {
  return slot.rotation === 90 || slot.rotation === 270 ? { width: slotSize.height, height: slotSize.width } : { width: slotSize.width, height: slotSize.height }
}

/** 格子在纸上占的矩形。 */
export function slotBox(slotSize: SheetSize, slot: SheetSlot): Box {
  return { x: slot.x, y: slot.y, ...footprint(slotSize, slot) }
}

export interface LayoutProblems {
  /** 全部问题，格子的问题带序号（从 1 开始，就是填充顺序）。和服务端保存前的检查一致。 */
  messages: string[]
  /** 越界或重叠的格子下标（从 0 开始），画布上标红。 */
  badSlots: Set<number>
}

/** 检查版式；没有问题时 messages 为空。 */
export function checkLayout(layout: SheetLayout): LayoutProblems {
  const messages: string[] = []
  const badSlots = new Set<number>()
  if (!CODE_PATTERN.test(layout.code)) messages.push('版式编码要以字母开头，只能有字母、数字、_、-，最长 64 个字符')
  if (!layout.name.trim() || layout.name.length > 128) messages.push('名称不能为空，最长 128 字')
  if (!inRange(layout.paper.width) || !inRange(layout.paper.height)) messages.push(`纸张宽高要在 0–${MAX_PAPER} mm 之间`)
  if (!inRange(layout.slot.width) || !inRange(layout.slot.height)) messages.push('格子宽高要大于 0')
  if (layout.slots.length === 0 || layout.slots.length > MAX_SLOTS) messages.push(`格子数要在 1–${MAX_SLOTS} 之间`)
  if (Math.abs(layout.offset.x) > 50 || Math.abs(layout.offset.y) > 50) messages.push('打印偏移要在 ±50 mm 以内')

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

/** 两个矩形是否重叠（贴边在误差以内不算）。 */
export function overlaps(a: Box, b: Box): boolean {
  return a.x < b.x + b.width - TOLERANCE && b.x < a.x + a.width - TOLERANCE && a.y < b.y + b.height - TOLERANCE && b.y < a.y + a.height - TOLERANCE
}

export interface ArrangeOptions {
  rows: number
  cols: number
  marginTop: number
  marginLeft: number
  gapX: number
  gapY: number
  /** 格子都转 90°（竖着放）。 */
  rotate: boolean
  order: FillOrder
}

/** 自动排列：按行列、页边距、间距排满，坐标保留 1 位小数。 */
export function arrange(slotSize: SheetSize, options: ArrangeOptions): SheetSlot[] {
  const rotation = options.rotate ? 90 : 0
  const size = footprint(slotSize, { x: 0, y: 0, rotation })
  const slots: SheetSlot[] = []
  const rows = Math.max(0, Math.floor(options.rows))
  const cols = Math.max(0, Math.floor(options.cols))
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

/** 纸上最多能排几行几列，页边距让整体居中（向下取到 0.1 mm）。 */
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
 */
export function renumber(slots: readonly SheetSlot[], order: FillOrder): SheetSlot[] {
  const primary = (slot: SheetSlot) => (order === 'rows' ? slot.y : slot.x)
  const secondary = (slot: SheetSlot) => (order === 'rows' ? slot.x : slot.y)
  const sorted = [...slots].sort((a, b) => primary(a) - primary(b) || secondary(a) - secondary(b))
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

/** 手动调整填充顺序：把第 from 个格子移到第 to 个（下标从 0 开始）。 */
export function moveSlot(slots: readonly SheetSlot[], from: number, to: number): SheetSlot[] {
  const list = slots.map((slot) => ({ ...slot }))
  if (from < 0 || from >= list.length) return list
  const [moving] = list.splice(from, 1)
  list.splice(Math.max(0, Math.min(list.length, to)), 0, moving)
  return list
}

/** 页数：第一页从第 startSlot 格开始，一页排满换下一页。和服务端一致。 */
export function pageCount(slotCount: number, labels: number, startSlot = 1): number {
  if (slotCount <= 0 || labels <= 0) return 0
  return Math.ceil((startSlot - 1 + labels) / slotCount)
}

/** 长度按 0.1 mm 取整，返回新对象。 */
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

/** 新建版式：按纸张和格子尺寸排满（不旋转、无间距、整体居中、先行后列）。 */
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

/** 默认的版式编码：A4_100x60_2x4 这样。 */
export function suggestCode(paperName: string, slot: SheetSize, rows: number, cols: number): string {
  const paper = /^A\d/.test(paperName) ? paperName.slice(0, 2) : 'SHEET'
  return `${paper}_${slot.width}x${slot.height}_${cols}x${rows}`.replace(/\./g, '_')
}

function inRange(value: number): boolean {
  return Number.isFinite(value) && value > 0 && value <= MAX_PAPER
}

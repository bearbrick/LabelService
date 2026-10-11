/**
 * 拼版设计器的状态：正在编辑的版式、选中的格子、撤销历史、格子里显示哪个模板的预览。
 * 版式的修改都通过这里的函数，改完调 commit() 记一次撤销历史（和模板设计器的 stores/designer.ts 一样）。
 */
import { computed, reactive } from 'vue'
import { templateThumbnail } from '../api/admin.ts'
import type { TemplateItem } from '../api/admin.ts'
import { createLayout, updateLayout } from '../api/layouts.ts'
import { alignElements, distributeElements } from '../editor/arrange.ts'
import type { AlignKind, Movable } from '../editor/arrange.ts'
import { History } from '../editor/history.ts'
import { checkLayout, footprint, normalizeLayout, slotBox } from '../model/layout.ts'
import type { FillOrder, LayoutView, SheetLayout } from '../model/layout.ts'
import { arrange, moveSlot, renumber } from '../model/layoutArrange.ts'
import type { ArrangeOptions } from '../model/layoutArrange.ts'
import { roundMm } from '../model/units.ts'
import { catalog } from './catalog.ts'

export const sheet = reactive({
  layout: null as SheetLayout | null,
  enabled: true,
  /**
   * 还没保存过（新建）。
   */
  isNew: false,
  /**
   * 上次保存时的快照，用来判断有没有未保存的改动。
   */
  saved: '',
  selection: [] as number[],
  /**
   * 格子里显示哪个模板的预览（尺寸和格子相同的已发布模板）。
   */
  previewCode: '',
  previewImage: null as HTMLImageElement | null,
  settings: { showGrid: true, grid: 1, snap: true },
  canUndo: false,
  canRedo: false,
  busy: false,
})

const history = new History()
const empty = { messages: [] as string[], badSlots: new Set<number>() }

export const problems = computed(() => (sheet.layout ? checkLayout(sheet.layout) : empty))
export const dirty = computed(() => !!sheet.layout && (sheet.isNew || snapshot() !== sheet.saved))

/**
 * 尺寸和格子相同、已经发布过的模板。
 */
export const matchingTemplates = computed<TemplateItem[]>(() => {
  const layout = sheet.layout
  if (!layout) return []
  return catalog.templates.filter((item) => item.latestVersion > 0 && Math.abs(item.width - layout.slot.width) <= 0.05 && Math.abs(item.height - layout.slot.height) <= 0.05)
})

/**
 * 打开一个版式开始编辑。
 */
export function openLayout(view: LayoutView, isNew = false, previewCode = ''): void {
  sheet.layout = normalizeLayout(view.layout)
  sheet.enabled = view.enabled
  sheet.isNew = isNew
  sheet.selection = []
  sheet.saved = isNew ? '' : snapshot()
  history.reset(snapshot())
  syncHistory()
  void setPreview(previewCode || matchingTemplates.value[0]?.code || '')
}

export function closeLayout(): void {
  sheet.layout = null
  sheet.selection = []
}

/**
 * 记录一次修改。
 */
export function commit(): void {
  if (!sheet.layout) return
  sheet.layout = normalizeLayout(sheet.layout)
  history.push(snapshot())
  syncHistory()
}

export function undo(): void {
  restore(history.undo())
}

export function redo(): void {
  restore(history.redo())
}

export function select(indices: number[]): void {
  sheet.selection = [...new Set(indices)].filter((index) => index >= 0 && index < (sheet.layout?.slots.length ?? 0)).sort((a, b) => a - b)
}

export function selectAll(): void {
  select(sheet.layout ? sheet.layout.slots.map((_, index) => index) : [])
}

/**
 * 从左侧拖入模板：在落点放一个新格子（落点是格子中心），加在填充顺序的最后。
 */
export function addSlotAt(x: number, y: number): void {
  const layout = sheet.layout
  if (!layout) return
  layout.slots.push({ x: roundMm(x - layout.slot.width / 2), y: roundMm(y - layout.slot.height / 2), rotation: 0 })
  select([layout.slots.length - 1])
  commit()
}

/**
 * 拖动结束：更新位置。
 */
export function moveSlots(updates: Array<{ index: number; x: number; y: number }>): void {
  const layout = sheet.layout
  if (!layout) return
  for (const update of updates) Object.assign(layout.slots[update.index], { x: update.x, y: update.y })
  commit()
}

/**
 * 方向键微调：每次 0.1 mm，按住 Shift 1 mm。
 */
export function nudge(dx: number, dy: number): void {
  const layout = sheet.layout
  if (!layout || !sheet.selection.length) return
  for (const index of sheet.selection) {
    layout.slots[index].x = roundMm(layout.slots[index].x + dx)
    layout.slots[index].y = roundMm(layout.slots[index].y + dy)
  }

  commit()
}

/**
 * 选中的格子顺时针转 90°，以格子中心为轴。
 */
export function rotateSelected(): void {
  const layout = sheet.layout
  if (!layout) return
  for (const index of sheet.selection) {
    const slot = layout.slots[index]
    const before = footprint(layout.slot, slot)
    slot.rotation = (slot.rotation + 90) % 360
    const after = footprint(layout.slot, slot)
    slot.x = roundMm(slot.x + (before.width - after.width) / 2)
    slot.y = roundMm(slot.y + (before.height - after.height) / 2)
  }

  commit()
}

export function removeSelected(): void {
  const layout = sheet.layout
  if (!layout || !sheet.selection.length) return
  const removing = new Set(sheet.selection)
  layout.slots = layout.slots.filter((_, index) => !removing.has(index))
  sheet.selection = []
  commit()
}

/**
 * 复制选中的格子，放在右下方 5 mm。
 */
export function duplicateSelected(): void {
  const layout = sheet.layout
  if (!layout || !sheet.selection.length) return
  const start = layout.slots.length
  for (const index of sheet.selection) layout.slots.push({ ...layout.slots[index], x: layout.slots[index].x + 5, y: layout.slots[index].y + 5 })
  select(layout.slots.slice(start).map((_, offset) => start + offset))
  commit()
}

/**
 * 对齐：选一个时对齐到纸张，多个时对齐到选区外框。
 */
export function alignSelected(kind: AlignKind): void {
  withBoxes((boxes) => alignElements(boxes, kind, sheet.layout!.paper))
}

/**
 * 等间距分布（至少 3 个）。
 */
export function distributeSelected(axis: 'h' | 'v'): void {
  withBoxes((boxes) => distributeElements(boxes, axis))
}

/**
 * 自动排列：替换全部格子。
 */
export function applyArrange(options: ArrangeOptions): void {
  const layout = sheet.layout
  if (!layout) return
  layout.slots = arrange(layout.slot, options)
  sheet.selection = []
  commit()
}

/**
 * 按位置重新编号。
 */
export function renumberSlots(order: FillOrder): void {
  const layout = sheet.layout
  if (!layout) return
  layout.slots = renumber(layout.slots, order)
  sheet.selection = []
  commit()
}

/**
 * 手动调整一个格子的填充顺序（下标从 0 开始）。
 */
export function setOrder(from: number, to: number): void {
  const layout = sheet.layout
  if (!layout || to === from || to < 0 || to >= layout.slots.length) return
  layout.slots = moveSlot(layout.slots, from, to)
  select([to])
  commit()
}

/**
 * 保存到服务端；有问题时不保存。返回是否成功（失败时抛出接口的错误）。
 */
export async function saveLayout(): Promise<boolean> {
  const layout = sheet.layout
  if (!layout || problems.value.messages.length) return false
  sheet.busy = true
  try {
    const view = sheet.isNew ? await createLayout(layout, sheet.enabled) : await updateLayout(layout, sheet.enabled)
    sheet.layout = normalizeLayout(view.layout)
    sheet.isNew = false
    sheet.saved = snapshot()
    return true
  } finally {
    sheet.busy = false
  }
}

/**
 * 换格子里显示的模板预览（服务端用样例值渲染的缩略图）。
 */
export async function setPreview(code: string): Promise<void> {
  sheet.previewCode = code
  sheet.previewImage = null
  const item = catalog.templates.find((template) => template.code === code)
  if (!item) return
  const image = await thumbnailImage(item.code, item.updatedAt)
  if (sheet.previewCode === code) sheet.previewImage = image
}

const thumbnails = new Map<string, Promise<HTMLImageElement | null>>()

function thumbnailImage(code: string, stamp: string): Promise<HTMLImageElement | null> {
  const key = `${code}|${stamp}`
  let pending = thumbnails.get(key)
  if (!pending) {
    pending = templateThumbnail(code, stamp)
      .then(async (response) => {
        if (response.status === 204) return null
        const image = new Image()
        image.src = URL.createObjectURL(await response.blob())
        await image.decode()
        return image
      })
      .catch(() => null)
    thumbnails.set(key, pending)
  }

  return pending
}

/**
 * 对选中格子所占的矩形做对齐、分布，再写回格子位置。
 */
function withBoxes(action: (boxes: Movable[]) => void): void {
  const layout = sheet.layout
  if (!layout || !sheet.selection.length) return
  const boxes = sheet.selection.map((index) => slotBox(layout.slot, layout.slots[index]))
  action(boxes)
  sheet.selection.forEach((index, i) => Object.assign(layout.slots[index], { x: roundMm(boxes[i].x), y: roundMm(boxes[i].y) }))
  commit()
}

function snapshot(): string {
  return JSON.stringify({ layout: sheet.layout, enabled: sheet.enabled })
}

function restore(value: string | null): void {
  if (value === null) return
  const state = JSON.parse(value) as { layout: SheetLayout; enabled: boolean }
  sheet.layout = state.layout
  sheet.enabled = state.enabled
  sheet.selection = sheet.selection.filter((index) => index < state.layout.slots.length)
  syncHistory()
}

function syncHistory(): void {
  sheet.canUndo = history.canUndo
  sheet.canRedo = history.canRedo
}

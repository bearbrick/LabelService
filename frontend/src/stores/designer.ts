/**
 * 设计器的状态和全部编辑操作。画布、面板、快捷键都调这里的函数，不直接改模板。
 * 每次修改后调用 commit() 记录撤销历史，并安排自动保存草稿：保存函数由模板工作区注册（PUT 草稿到服务端）。
 * 只读（没有编辑权限）时 commit() 会撤回刚才的修改。
 */
import { computed, reactive } from 'vue'
import { alignElements, distributeElements, moveAbove, reorder } from '../editor/arrange.ts'
import type { AlignKind, OrderKind } from '../editor/arrange.ts'
import { readClipboard, writeClipboard } from '../editor/clipboard.ts'
import type { Metric } from '../editor/draw.ts'
import { History } from '../editor/history.ts'
import type { DisplayMode } from '../model/bindings.ts'
import { renameKey } from '../model/bindings.ts'
import { cloneElements, makeElement, normalizeElement } from '../model/elements.ts'
import type { ElementType, FieldDefinition, FieldType, LabelElement, LabelTemplate } from '../model/template.ts'
import { roundMm } from '../model/units.ts'
import { checkTemplate, FIELD_KEY_PATTERN } from '../model/validate.ts'
import sample from '../samples/box-label.json'

/** 画布提供给 store 的能力（由 CanvasWorkspace 注册）。 */
export interface CanvasController {
  viewCenter(): { x: number; y: number }
  zoomBy(factor: number): void
  zoomTo(zoom: number): void
  zoomFit(): void
  requestFit(): void
  editText(id: string): void
  /** 把纸张导出成指定像素尺寸的白底图片（和服务端预览对比用）。 */
  snapshot(width: number, height: number): HTMLCanvasElement | null
}

export interface Toast {
  id: number
  text: string
  error: boolean
}

/** 保存草稿的函数（由模板工作区注册），失败时抛错。 */
export type DraftSaver = (template: LabelTemplate) => Promise<void>

const history = new History(100)
let canvas: CanvasController | null = null
let commitTimer: ReturnType<typeof setTimeout> | undefined
let saveTimer: ReturnType<typeof setTimeout> | undefined
let saver: DraftSaver | null = null
let saving: Promise<boolean> = Promise.resolve(true)
let toastId = 0
let pasteCount = 0

export const designer = reactive({
  template: copyOf(sample as unknown as LabelTemplate),
  selection: [] as string[],
  metrics: {} as Record<string, Metric>,
  canUndo: false,
  canRedo: false,
  dirty: false,
  saving: false,
  saveError: '',
  savedAt: '',
  readOnly: false,
  renderVersion: 0,
  toasts: [] as Toast[],
  ui: {
    mode: 'sample' as DisplayMode,
    showGrid: true,
    gridSize: 1,
    snap: true,
    zoom: 1,
    cursorX: null as number | null,
    cursorY: null as number | null,
    spaceDown: false,
    leftTab: 'elements' as 'elements' | 'fields' | 'layers',
    tip: '',
    dropActive: false,
    shortcutsOpen: false,
  },
})

export const selectedElements = computed(() =>
  designer.selection.map((id) => designer.template.elements.find((element) => element.id === id)).filter((element): element is LabelElement => !!element),
)
export const selectedElement = computed(() => (selectedElements.value.length === 1 ? selectedElements.value[0] : null))
export const allSelectedLocked = computed(() => selectedElements.value.length > 0 && selectedElements.value.every((element) => element.locked))
export const report = computed(() => checkTemplate(designer.template))

history.reset(snapshot())

export function registerCanvas(controller: CanvasController | null): void {
  canvas = controller
}

export function toast(text: string, error = false): void {
  const id = ++toastId
  designer.toasts.push({ id, text, error })
  setTimeout(() => {
    const index = designer.toasts.findIndex((item) => item.id === id)
    if (index >= 0) designer.toasts.splice(index, 1)
  }, error ? 4200 : 2600)
}

/* ---------------- 历史与保存 ---------------- */

function snapshot(): string {
  return JSON.stringify(designer.template)
}

/** 记录一次修改（和上一步相同则忽略），并安排保存草稿。 */
export function commit(): void {
  clearTimeout(commitTimer)
  commitTimer = undefined
  if (designer.readOnly) {
    if (history.push(snapshot())) {
      restore(history.undo())
      history.dropRedo()
      syncHistoryFlags()
      toast('只读：当前账号不能编辑这个模板', true)
    }
    return
  }

  history.push(snapshot())
  syncHistoryFlags()
  scheduleSave()
}

/** 连续的小改动（方向键微调、输入框）合并成一步。 */
export function commitSoon(): void {
  clearTimeout(commitTimer)
  commitTimer = setTimeout(commit, 400)
}

export function undo(): void {
  if (commitTimer) commit()
  restore(history.undo())
}

export function redo(): void {
  restore(history.redo())
}

function restore(state: string | null): void {
  if (state === null) return
  designer.template = JSON.parse(state) as LabelTemplate
  designer.selection = designer.selection.filter((id) => designer.template.elements.some((element) => element.id === id))
  syncHistoryFlags()
  scheduleSave()
}

function syncHistoryFlags(): void {
  designer.canUndo = history.canUndo
  designer.canRedo = history.canRedo
}

function scheduleSave(): void {
  if (designer.readOnly) return
  designer.dirty = true
  clearTimeout(saveTimer)
  saveTimer = setTimeout(() => void saveDraft(), 1000)
}

/** 注册保存草稿的函数（模板工作区打开模板时注册，离开时传 null）。 */
export function setDraftSaver(value: DraftSaver | null): void {
  saver = value
}

/** 立即保存草稿（有未保存的修改时）。多次调用按顺序执行；返回是否已全部保存。 */
export function saveDraft(): Promise<boolean> {
  if (commitTimer) commit()
  clearTimeout(saveTimer)
  saving = saving.then(saveNow)
  return saving
}

async function saveNow(): Promise<boolean> {
  if (!designer.dirty || !saver || designer.readOnly) return !designer.dirty
  const text = snapshot()
  designer.saving = true
  try {
    await saver(JSON.parse(text) as LabelTemplate)
    if (snapshot() === text) designer.dirty = false
    designer.saveError = ''
    designer.savedAt = new Date().toLocaleTimeString('zh-CN', { hour12: false })
    return !designer.dirty
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    if (designer.saveError !== message) toast(`草稿保存失败：${message}`, true)
    designer.saveError = message
    return false
  } finally {
    designer.saving = false
  }
}

function copyOf(template: LabelTemplate): LabelTemplate {
  const copy = JSON.parse(JSON.stringify(template)) as LabelTemplate
  copy.fields ??= []
  copy.elements = (copy.elements ?? []).map((element) => normalizeElement(element))
  copy.page.dpi ??= 300
  return copy
}

/* ---------------- 模板 ---------------- */

/** 打开一个模板（从服务端读到的草稿或版本），清空撤销历史。readOnly 为真时不能修改。 */
export function openTemplate(template: LabelTemplate, readOnly = false): void {
  clearTimeout(commitTimer)
  clearTimeout(saveTimer)
  commitTimer = undefined
  designer.template = copyOf(template)
  designer.selection = []
  designer.metrics = {}
  designer.readOnly = readOnly
  designer.dirty = false
  designer.saveError = ''
  designer.savedAt = ''
  history.reset(snapshot())
  syncHistoryFlags()
  canvas?.requestFit()
}

/** 导入模板 JSON 文本，替换当前模板的画布、字段和元素（编码和名称不变，可以撤销）；格式不对时抛出带中文说明的错误。 */
export function importTemplate(json: string): void {
  let parsed: Partial<LabelTemplate>
  try {
    parsed = JSON.parse(json) as Partial<LabelTemplate>
  } catch {
    throw new Error('不是合法的 JSON')
  }

  if (!parsed.page || !(parsed.page.width > 0) || !(parsed.page.height > 0) || !Array.isArray(parsed.elements)) {
    throw new Error('缺少 page 或 elements，不是模板 JSON')
  }

  const imported = copyOf({ schemaVersion: '1.0', code: '', name: '', fields: [], ...parsed } as LabelTemplate)
  designer.template.page = imported.page
  designer.template.fields = imported.fields
  designer.template.elements = imported.elements
  designer.selection = []
  commit()
  canvas?.requestFit()
}

/** 导出模板 JSON（缩进 2 格）。有不能导出的问题时抛错。 */
export function exportTemplate(): string {
  const problems = report.value.errors
  if (problems.length) throw new Error(problems[0])
  return JSON.stringify(designer.template, null, 2)
}

export function setPage(width: number, height: number): void {
  designer.template.page.width = roundMm(width)
  designer.template.page.height = roundMm(height)
  commit()
  canvas?.zoomFit()
}

/** 当前画布的纸张快照；设计器没打开时返回 null。 */
export function canvasSnapshot(width: number, height: number): HTMLCanvasElement | null {
  return canvas?.snapshot(width, height) ?? null
}

/* ---------------- 选择 ---------------- */

export function select(ids: string[]): void {
  designer.selection = ids
}

export function selectAll(): void {
  designer.selection = designer.template.elements.filter((element) => !element.hidden).map((element) => element.id)
}

/* ---------------- 元素 ---------------- */

/** 新建元素：放在指定位置（拖入时）或视图中心，并限制在画布内。 */
export function addElement(type: ElementType, extra: Partial<LabelElement> = {}, at?: { x: number; y: number }): LabelElement {
  const template = designer.template
  const element = makeElement(type, template.fields, template.elements, extra)
  const center = at ?? canvas?.viewCenter() ?? { x: template.page.width / 2, y: template.page.height / 2 }
  element.x = roundMm(Math.min(Math.max(center.x - element.width / 2, 0), Math.max(0, template.page.width - element.width)))
  element.y = roundMm(Math.min(Math.max(center.y - element.height / 2, 0), Math.max(0, template.page.height - element.height)))
  template.elements.push(element)
  designer.selection = [element.id]
  commit()
  return element
}

export function removeSelected(): void {
  const unlocked = selectedElements.value.filter((element) => !element.locked)
  if (!unlocked.length) {
    if (selectedElements.value.length) toast('选中的元素已锁定，先解锁再删除', true)
    return
  }

  const ids = new Set(unlocked.map((element) => element.id))
  const kept = selectedElements.value.length - unlocked.length
  designer.template.elements = designer.template.elements.filter((element) => !ids.has(element.id))
  designer.selection = designer.selection.filter((id) => !ids.has(id))
  commit()
  if (kept) toast(`已删除 ${ids.size} 个，${kept} 个锁定的元素保留`)
}

/** 原地复制一份，偏移 2mm。 */
export function duplicate(): void {
  if (!selectedElements.value.length) return
  const copies = cloneElements(selectedElements.value, 2, designer.template.elements)
  copies.forEach((copy) => (copy.name = `${copy.name} 副本`))
  designer.template.elements.push(...copies)
  designer.selection = copies.map((copy) => copy.id)
  commit()
}

export function copySelection(): void {
  if (!selectedElements.value.length) return
  writeClipboard(selectedElements.value)
  pasteCount = 0
  toast(`已复制 ${selectedElements.value.length} 个元素，可以粘贴到其他模板`)
}

export function paste(): void {
  const elements = readClipboard()
  if (!elements) return
  pasteCount++
  const copies = cloneElements(elements, 2 * pasteCount, designer.template.elements)
  designer.template.elements.push(...copies)
  designer.selection = copies.map((copy) => copy.id)
  commit()
}

export function align(kind: AlignKind): void {
  alignElements(selectedElements.value, kind, designer.template.page)
  commit()
}

export function distribute(axis: 'h' | 'v'): void {
  distributeElements(selectedElements.value, axis)
  commit()
}

export function order(kind: OrderKind): void {
  if (!designer.selection.length) return
  designer.template.elements = reorder(designer.template.elements, new Set(designer.selection), kind)
  commit()
}

export function moveLayer(movingId: string, targetId: string): void {
  designer.template.elements = moveAbove(designer.template.elements, movingId, targetId)
  commit()
}

export function toggleLockSelection(): void {
  const lock = !allSelectedLocked.value
  selectedElements.value.forEach((element) => (element.locked = lock))
  commit()
  toast(lock ? '已锁定，不能拖动和缩放' : '已解锁')
}

export function toggleLock(element: LabelElement): void {
  element.locked = !element.locked
  commit()
}

export function toggleHidden(element: LabelElement): void {
  element.hidden = !element.hidden
  if (element.hidden) designer.selection = designer.selection.filter((id) => id !== element.id)
  commit()
}

/** 方向键微调：0.1mm，按住 Shift 1mm。 */
export function nudge(dx: number, dy: number): void {
  for (const element of selectedElements.value) {
    if (element.locked) continue
    element.x = roundMm(element.x + dx)
    element.y = roundMm(element.y + dy)
  }

  commitSoon()
}

export function setLineDirection(element: LabelElement, direction: 'h' | 'v'): void {
  const length = Math.max(element.width, element.height) || 30
  element.width = direction === 'h' ? length : 0
  element.height = direction === 'h' ? 0 : length
  element.rotation = 0
  commit()
}

/** 修改旋转角度；条码只允许 0、90、180、270。 */
export function setRotation(element: LabelElement, degrees: number): void {
  let rotation = ((Number(degrees) || 0) % 360 + 360) % 360
  if (element.type === 'barcode' && rotation % 90) {
    rotation = (Math.round(rotation / 90) * 90) % 360
    toast('条码只能旋转 0°、90°、180°、270°', true)
  }

  element.rotation = roundMm(rotation)
  commit()
}

export function editText(id: string): void {
  canvas?.editText(id)
}

/* ---------------- 视图 ---------------- */

export function zoomIn(): void {
  canvas?.zoomBy(1.25)
}

export function zoomOut(): void {
  canvas?.zoomBy(0.8)
}

export function zoomActual(): void {
  canvas?.zoomTo(1)
}

export function zoomFit(): void {
  canvas?.zoomFit()
}

/* ---------------- 字段 ---------------- */

/** 添加字段；key 不合法或重复时返回错误说明。 */
export function addField(key: string, type: FieldType, name?: string): string | null {
  const trimmed = key.trim()
  if (!FIELD_KEY_PATTERN.test(trimmed)) return 'key 要以字母开头，只含字母、数字、下划线'
  if (designer.template.fields.some((field) => field.key === trimmed)) return `字段 ${trimmed} 已存在`
  designer.template.fields.push({ key: trimmed, name: name || undefined, type, required: true })
  commit()
  return null
}

/** 改字段 key，模板里引用它的元素一起改；新 key 不合法或重复时返回错误说明。 */
export function renameField(field: FieldDefinition, key: string): string | null {
  const trimmed = key.trim()
  if (trimmed === field.key) return null
  if (!FIELD_KEY_PATTERN.test(trimmed)) return 'key 要以字母开头，只含字母、数字、下划线'
  if (designer.template.fields.some((item) => item !== field && item.key === trimmed)) return `字段 ${trimmed} 已存在`
  const from = field.key
  field.key = trimmed
  for (const element of designer.template.elements) {
    if (element.type === 'text') element.content = renameKey(element.content, from, trimmed)
    else if (element.type === 'barcode' || element.type === 'qrcode') element.value = renameKey(element.value, from, trimmed)
    else if (element.type === 'image') element.src = renameKey(element.src, from, trimmed)
  }

  commit()
  return null
}

export function removeField(field: FieldDefinition): void {
  designer.template.fields = designer.template.fields.filter((item) => item !== field)
  commit()
}

/** 把字段插入选中的文本，或绑定到选中的条码。 */
export function insertFieldIntoSelection(key: string): void {
  const element = selectedElement.value
  if (element?.type === 'text') {
    element.content += `{{${key}}}`
    commit()
    toast(`已插入 {{${key}}}`)
  } else if (element?.type === 'barcode' || element?.type === 'qrcode') {
    element.value = `{{${key}}}`
    commit()
    toast(`已绑定 {{${key}}}`)
  } else {
    toast('先选中一个文本或条码元素', true)
  }
}

/** 绘制时回报的提示，内容没变时不写，避免多余的界面刷新。 */
export function setMetric(id: string, metric: Metric): void {
  const current = designer.metrics[id]
  if (current && current.text === metric.text && current.tone === metric.tone) return
  designer.metrics[id] = metric
}

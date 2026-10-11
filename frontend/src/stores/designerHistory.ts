/**
 * 设计器的撤销历史和自动保存：commit() 记录一次修改并安排 1 秒后保存草稿，commitSoon() 合并连续的小改动。
 * 只读（没有编辑权限）时 commit() 会撤回刚才的修改。保存函数由模板工作区注册（PUT 草稿到服务端）。
 */
import { History } from '../editor/history.ts'
import type { LabelTemplate } from '../model/template.ts'
import { designer, toast } from './designerState.ts'
import type { DraftSaver } from './designerState.ts'

/**
 * 撤销历史，最多 100 步。
 */
const history = new History(100)

/**
 * commitSoon 合并连续改动的计时器。
 */
let commitTimer: ReturnType<typeof setTimeout> | undefined

/**
 * 自动保存草稿的计时器（改完 1 秒后保存）。
 */
let saveTimer: ReturnType<typeof setTimeout> | undefined

/**
 * 保存草稿的函数；没注册时不保存。
 */
let saver: DraftSaver | null = null

/**
 * 保存队列：多次保存按顺序执行。
 */
let saving: Promise<boolean> = Promise.resolve(true)

/**
 * 当前模板的 JSON 快照（撤销历史和保存用）。
 * @returns JSON 文本。
 */
function snapshot(): string {
  return JSON.stringify(designer.template)
}

/**
 * 记录一次修改（和上一步相同则忽略），并安排保存草稿。
 */
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

/**
 * 连续的小改动（方向键微调、输入框）合并成一步。
 */
export function commitSoon(): void {
  clearTimeout(commitTimer)
  commitTimer = setTimeout(commit, 400)
}

/**
 * 撤销一步（有还没合并的连续改动时先记下它）。
 */
export function undo(): void {
  if (commitTimer) commit()
  restore(history.undo())
}

/**
 * 重做一步。
 */
export function redo(): void {
  restore(history.redo())
}

/**
 * 恢复到某个快照，去掉已不存在的选中元素，并安排保存。
 * @param state 快照；null 时什么也不做。
 */
function restore(state: string | null): void {
  if (state === null) return
  designer.template = JSON.parse(state) as LabelTemplate
  designer.selection = designer.selection.filter((id) => designer.template.elements.some((element) => element.id === id))
  syncHistoryFlags()
  scheduleSave()
}

/**
 * 把撤销、重做是否可用同步到状态（工具栏按钮用）。
 */
function syncHistoryFlags(): void {
  designer.canUndo = history.canUndo
  designer.canRedo = history.canRedo
}

/**
 * 标记有未保存的修改，1 秒后自动保存草稿（只读时不保存）。
 */
function scheduleSave(): void {
  if (designer.readOnly) return
  designer.dirty = true
  clearTimeout(saveTimer)
  saveTimer = setTimeout(() => void saveDraft(), 1000)
}

/**
 * 注册保存草稿的函数（模板工作区打开模板时注册，离开时传 null）。
 * @param value 保存函数；null 表示不再保存。
 */
export function setDraftSaver(value: DraftSaver | null): void {
  saver = value
}

/**
 * 立即保存草稿（有未保存的修改时）。多次调用按顺序执行；返回是否已全部保存。
 * @returns 全部保存成功时为 true。
 */
export function saveDraft(): Promise<boolean> {
  if (commitTimer) commit()
  clearTimeout(saveTimer)
  saving = saving.then(saveNow)
  return saving
}

/**
 * 保存一次：保存期间又有修改时仍标记为未保存；失败时提示（同样的错误只提示一次）。
 * @returns 保存后没有未保存的修改时为 true。
 */
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

/**
 * 丢掉还没执行的合并和保存计时器，以当前模板作为撤销历史的新起点（打开模板时用）。
 */
export function resetHistory(): void {
  clearTimeout(commitTimer)
  clearTimeout(saveTimer)
  commitTimer = undefined
  history.reset(snapshot())
  syncHistoryFlags()
}

// 以默认模板（示例箱标）作为撤销历史的起点。
history.reset(snapshot())

/**
 * 设计器的剪贴板：复制的元素存在 localStorage，换一个模板（或刷新页面）也能粘贴。
 * 浏览器禁止本地存储时退回到内存。
 */
import type { LabelElement } from '../model/template.ts'

/**
 * localStorage 里的键名。
 */
const STORAGE_KEY = 'label-designer.clipboard'

/**
 * 内存里的副本（本地存储不可用时用）。
 */
let memory: string | null = null

/**
 * 剪贴板里存的内容。
 */
interface ClipboardPayload {
  /**
   * 内容类型，读的时候核对。
   */
  kind: 'label-elements'
  /**
   * 复制的元素。
   */
  elements: readonly LabelElement[]
}

/**
 * 把元素放进剪贴板。
 * @param elements 复制的元素。
 */
export function writeClipboard(elements: readonly LabelElement[]): void {
  const json = JSON.stringify({ kind: 'label-elements', elements } satisfies ClipboardPayload)
  memory = json
  try {
    globalThis.localStorage?.setItem(STORAGE_KEY, json)
  } catch {
    // 本地存储不可用（隐私模式等），只保留在内存里。
  }
}

/**
 * 读剪贴板里的元素。
 * @returns 元素的副本；没有或内容不对时为 null。
 */
export function readClipboard(): LabelElement[] | null {
  let json = memory
  try {
    json = globalThis.localStorage?.getItem(STORAGE_KEY) ?? json
  } catch {
    // 同上，读内存里的副本。
  }

  if (!json) return null
  try {
    const payload = JSON.parse(json) as Partial<ClipboardPayload>
    return payload.kind === 'label-elements' && Array.isArray(payload.elements) && payload.elements.length ? [...payload.elements] : null
  } catch {
    return null
  }
}

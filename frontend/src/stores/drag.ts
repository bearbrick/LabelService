/**
 * 从左侧面板拖到画布的内容。HTML5 拖放在 dragover 阶段读不到 dataTransfer 的数据，所以放在这里。
 */
import type { ElementType } from '../model/template.ts'

export type DragPayload =
  | { kind: 'element'; type: ElementType }
  | { kind: 'icon'; icon: string; name: string }
  | { kind: 'asset'; id: string; name: string }
  | { kind: 'field'; key: string }

export const drag: { payload: DragPayload | null } = { payload: null }

export function startDrag(event: DragEvent, payload: DragPayload): void {
  drag.payload = payload
  if (!event.dataTransfer) return
  event.dataTransfer.effectAllowed = 'copy'
  try {
    event.dataTransfer.setData('text/plain', JSON.stringify(payload))
  } catch {
    // 个别浏览器不允许自定义数据，不影响拖放。
  }
}

export function endDrag(): void {
  drag.payload = null
}

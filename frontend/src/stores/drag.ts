/**
 * 从左侧面板拖到画布的内容。HTML5 拖放在 dragover 阶段读不到 dataTransfer 的数据，所以放在这里。
 */
import type { ElementType } from '../model/template.ts'

/**
 * 拖动的内容。
 */
export type DragPayload =
  | {
      /**
       * 元素库里的基础元素。
       */
      kind: 'element'
      /**
       * 元素类型。
       */
      type: ElementType
    }
  | {
      /**
       * 图标库里的图标。
       */
      kind: 'icon'
      /**
       * 图标路径，如 gb191/fragile。
       */
      icon: string
      /**
       * 图标名称（新元素的名字）。
       */
      name: string
    }
  | {
      /**
       * 素材库里的图片。
       */
      kind: 'asset'
      /**
       * 素材 key。
       */
      id: string
      /**
       * 素材名称（新元素的名字）。
       */
      name: string
    }
  | {
      /**
       * 字段列表里的字段（拖到条码上绑定，拖到空白处生成文本）。
       */
      kind: 'field'
      /**
       * 字段 key。
       */
      key: string
    }

/**
 * 当前拖动的内容；没在拖动时为 null。
 */
export const drag: { payload: DragPayload | null } = { payload: null }

/**
 * 开始拖动：记下内容，并写进 dataTransfer（允许复制）。
 * @param event 拖动开始的事件。
 * @param payload 拖动的内容。
 */
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

/**
 * 拖动结束（放下或取消）。
 */
export function endDrag(): void {
  drag.payload = null
}

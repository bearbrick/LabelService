/**
 * 左侧模板树的宽度：拖动右边缘改宽度，双击恢复默认，键盘左右方向键每次 16 像素。宽度记在浏览器里（只影响自己），刷新后保持。
 */
import { ref } from 'vue'

/**
 * 默认宽度，px。
 */
export const SIDEBAR_DEFAULT_WIDTH = 280

/**
 * 最小宽度，px：再窄搜索框和按钮放不下。
 */
const MIN_WIDTH = 220

/**
 * 最大宽度，px；同时不超过窗口宽度减去 600（给右边的页面留位置）。
 */
const MAX_WIDTH = 560

/**
 * 键盘每按一次改变的宽度，px。
 */
const KEY_STEP = 16

/**
 * 浏览器本地存储里的键。
 */
const STORAGE_KEY = 'label-admin.sidebar-width'

/**
 * 把宽度限制在上下限之间。
 * @param width 想要的宽度，px。
 * @returns 限制后的宽度，px。
 */
function clampWidth(width: number): number {
  const max = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, window.innerWidth - 600))
  return Math.round(Math.min(max, Math.max(MIN_WIDTH, width)))
}

/**
 * 读上次保存的宽度；没有或读不到时用默认宽度。
 * @returns 宽度，px。
 */
function loadWidth(): number {
  try {
    const saved = Number(localStorage.getItem(STORAGE_KEY))
    return saved > 0 ? clampWidth(saved) : SIDEBAR_DEFAULT_WIDTH
  } catch {
    // 隐私模式或禁用了站点数据时读不到，用默认宽度。
    return SIDEBAR_DEFAULT_WIDTH
  }
}

/**
 * 保存宽度；存不进去时忽略（下次用默认宽度）。
 * @param width 宽度，px。
 */
function saveWidth(width: number): void {
  try {
    localStorage.setItem(STORAGE_KEY, String(width))
  } catch {
    // 存不进去不影响使用。
  }
}

/**
 * 模板树宽度和拖动手柄的事件处理。
 * @returns 当前宽度、开始拖动、双击恢复、键盘调整。
 */
export function useSidebarWidth() {
  /**
   * 当前宽度，px。
   */
  const width = ref(loadWidth())

  /**
   * 正在拖动。
   */
  const dragging = ref(false)

  /**
   * 按下手柄开始拖动：移动时按水平位移改宽度，松开时保存。监听整个窗口，鼠标移出手柄也能继续拖。
   * @param event 按下指针的事件。
   */
  function startDrag(event: PointerEvent): void {
    if (event.button !== 0) return
    event.preventDefault()
    const startX = event.clientX
    const startWidth = width.value
    dragging.value = true
    const move = (e: PointerEvent) => (width.value = clampWidth(startWidth + e.clientX - startX))
    const up = () => {
      dragging.value = false
      window.removeEventListener('pointermove', move)
      window.removeEventListener('pointerup', up)
      window.removeEventListener('pointercancel', up)
      saveWidth(width.value)
    }
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', up)
    window.addEventListener('pointercancel', up)
  }

  /**
   * 双击手柄恢复默认宽度。
   */
  function reset(): void {
    width.value = SIDEBAR_DEFAULT_WIDTH
    saveWidth(width.value)
  }

  /**
   * 手柄获得焦点时用左右方向键调宽度，Home 恢复默认。
   * @param event 键盘事件。
   */
  function onKey(event: KeyboardEvent): void {
    const delta = event.key === 'ArrowLeft' ? -KEY_STEP : event.key === 'ArrowRight' ? KEY_STEP : 0
    if (event.key === 'Home') {
      event.preventDefault()
      reset()
    } else if (delta) {
      event.preventDefault()
      width.value = clampWidth(width.value + delta)
      saveWidth(width.value)
    }
  }

  return { width, dragging, startDrag, reset, onKey, minWidth: MIN_WIDTH, maxWidth: MAX_WIDTH }
}

<script setup lang="ts">
/**
 * 设计器：工具栏、左侧面板、画布、属性面板、状态栏，以及全部键盘快捷键。
 */
import { onBeforeUnmount, onMounted } from 'vue'
import {
  copySelection,
  designer,
  duplicate,
  editText,
  nudge,
  order,
  paste,
  redo,
  removeSelected,
  saveDraft,
  select,
  selectAll,
  selectedElement,
  toast,
  toggleLockSelection,
  undo,
  zoomActual,
  zoomFit,
  zoomIn,
  zoomOut,
} from '../stores/designer.ts'
import CanvasWorkspace from './CanvasWorkspace.vue'
import DesignerToolbar from './DesignerToolbar.vue'
import LeftPanel from './LeftPanel.vue'
import PropertyPanel from './PropertyPanel.vue'
import ShortcutsDialog from './ShortcutsDialog.vue'
import StatusBar from './StatusBar.vue'

/**
 * 设计器的全部快捷键（见快捷键说明对话框）：Ctrl+S 保存；在输入框里打字时只响应 Ctrl+S；
 * 其余按表格逐条匹配，第一条匹配的执行；方向键微调选中的元素（Shift 时 1 mm）。
 * @param event 键盘事件。
 */
function onKeyDown(event: KeyboardEvent): void {
  if (designer.ui.shortcutsOpen) {
    if (event.key === 'Escape') designer.ui.shortcutsOpen = false
    return
  }

  const target = event.target as HTMLElement | null
  const typing = !!target && (['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) || target.isContentEditable)
  const mod = event.ctrlKey || event.metaKey
  const key = event.key.toLowerCase()
  // Ctrl+S 在输入框里也有效：立即保存草稿。
  if (mod && key === 's') {
    event.preventDefault()
    void saveDraft().then((ok) => ok && toast(designer.readOnly ? '只读模板不保存' : '草稿已保存'))
    return
  }

  // 其余快捷键在输入框里打字时不响应，免得和输入冲突。
  if (typing) return
  // 快捷键表：按顺序找第一条匹配的执行（和快捷键说明对话框一致）。
  const actions: Array<[boolean, () => void]> = [
    [event.key === ' ', () => (designer.ui.spaceDown = true)],
    [mod && !event.shiftKey && key === 'z', undo],
    [mod && (key === 'y' || (event.shiftKey && key === 'z')), redo],
    [mod && key === 'c', copySelection],
    [mod && key === 'v', paste],
    [mod && key === 'd', duplicate],
    [mod && key === 'a', selectAll],
    [mod && key === 'l', () => designer.selection.length && toggleLockSelection()],
    [mod && (key === '=' || key === '+'), zoomIn],
    [mod && key === '-', zoomOut],
    [mod && key === '0', zoomFit],
    [mod && key === '1', zoomActual],
    [mod && event.code === 'BracketRight', () => order(event.shiftKey ? 'front' : 'up')],
    [mod && event.code === 'BracketLeft', () => order(event.shiftKey ? 'back' : 'down')],
    [event.key === 'Delete' || event.key === 'Backspace', removeSelected],
    [event.key === 'Escape', () => select([])],
    [event.key === 'Enter' && selectedElement.value?.type === 'text', () => editText(selectedElement.value!.id)],
  ]
  const hit = actions.find(([matches]) => matches)
  if (hit) {
    event.preventDefault()
    hit[1]()
    return
  }

  // 方向键微调选中的元素：每次 0.1 mm，按住 Shift 1 mm。
  const step = event.shiftKey ? 1 : 0.1
  const arrows: Record<string, [number, number]> = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] }
  if (arrows[event.key] && designer.selection.length) {
    event.preventDefault()
    nudge(...arrows[event.key])
  }
}

/**
 * 松开空格时结束平移模式。
 * @param event 键盘事件。
 */
function onKeyUp(event: KeyboardEvent): void {
  if (event.key === ' ') designer.ui.spaceDown = false
}

/**
 * 窗口失去焦点时结束平移模式（收不到松开空格的事件）。
 */
function onBlur(): void {
  designer.ui.spaceDown = false
}

onMounted(() => {
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  window.addEventListener('blur', onBlur)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  window.removeEventListener('keyup', onKeyUp)
  window.removeEventListener('blur', onBlur)
})
</script>

<template>
  <div class="designer">
    <DesignerToolbar />
    <LeftPanel />
    <CanvasWorkspace />
    <PropertyPanel />
    <StatusBar />
    <ShortcutsDialog />
    <div class="narrow-note">
      <div class="in">
        <b>窗口太窄，放不下设计器</b>
        <span>请把窗口加宽到 1100px 以上。</span>
      </div>
    </div>
  </div>
</template>

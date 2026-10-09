<script setup lang="ts">
/** 设计器：工具栏、左侧面板、画布、属性面板、状态栏，以及全部键盘快捷键。 */
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

function onKeyDown(event: KeyboardEvent): void {
  if (designer.ui.shortcutsOpen) {
    if (event.key === 'Escape') designer.ui.shortcutsOpen = false
    return
  }

  const target = event.target as HTMLElement | null
  const typing = !!target && (['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) || target.isContentEditable)
  const mod = event.ctrlKey || event.metaKey
  const key = event.key.toLowerCase()
  if (mod && key === 's') {
    event.preventDefault()
    saveDraft()
    toast('草稿已保存到本机')
    return
  }

  if (typing) return
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

  const step = event.shiftKey ? 1 : 0.1
  const arrows: Record<string, [number, number]> = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] }
  if (arrows[event.key] && designer.selection.length) {
    event.preventDefault()
    nudge(...arrows[event.key])
  }
}

function onKeyUp(event: KeyboardEvent): void {
  if (event.key === ' ') designer.ui.spaceDown = false
}

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

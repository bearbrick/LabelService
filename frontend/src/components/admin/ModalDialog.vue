<script setup lang="ts">
/**
 * 弹窗外框：标题、内容、底部按钮。点遮罩或按 Esc 关闭。
 */
import { onBeforeUnmount, onMounted } from 'vue'
import AppIcon from '../AppIcon.vue'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 标题。
   */
  title: string
  /**
   * 宽版（表单较多时）。
   */
  wide?: boolean
  /**
   * 指定宽度（CSS 写法，如 720px），优先于 wide。
   */
  width?: string
}>()
/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 要关闭（点遮罩、关闭按钮或按 Esc）。
   */
  close: []
}>()

/**
 * 按 Esc 关闭（捕获阶段处理，不让设计器的快捷键收到）。
 * @param event 键盘事件。
 */
function onKey(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return
  event.stopPropagation()
  emit('close')
}

onMounted(() => window.addEventListener('keydown', onKey, true))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey, true))
</script>

<template>
  <div class="modal-back" @mousedown.self="emit('close')">
    <div class="modal" :class="{ wide: props.wide }" role="dialog" aria-modal="true" :aria-label="props.title" :style="props.width ? { width: props.width } : undefined">
      <div class="modal-h">
        <h3>{{ props.title }}</h3>
        <button class="icon-btn" type="button" aria-label="关闭" @click="emit('close')"><AppIcon name="x" /></button>
      </div>
      <div class="modal-b"><slot /></div>
      <div v-if="$slots.footer" class="modal-f"><slot name="footer" /></div>
    </div>
  </div>
</template>

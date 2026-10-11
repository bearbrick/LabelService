<script setup lang="ts">
import { computed } from 'vue'
import { designer } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'

/**
 * 光标位置，如“X 12.3 · Y 45.6 mm”。
 */
const cursor = computed(() => {
  const format = (value: number | null) => (value === null ? '—' : value.toFixed(1))
  return `X ${format(designer.ui.cursorX)} · Y ${format(designer.ui.cursorY)} mm`
})

/**
 * 草稿的保存状态：只读、保存失败、正在保存、已保存的时间。
 */
const saved = computed(() => {
  if (designer.readOnly) return '只读'
  if (designer.saveError) return `草稿保存失败：${designer.saveError}`
  if (designer.dirty) return '有修改，正在保存草稿…'
  return designer.savedAt ? `草稿已保存到服务端 ${designer.savedAt}` : ''
})
</script>

<template>
  <div class="statusbar">
    <span class="num">{{ cursor }}</span>
    <span class="num">缩放 {{ Math.round(designer.ui.zoom * 100) }}%</span>
    <span>{{ designer.template.elements.length }} 个元素<template v-if="designer.selection.length"> · 已选 {{ designer.selection.length }}</template></span>
    <span class="spacer"></span>
    <span>{{ saved }}</span>
    <button class="sb-btn" type="button" @click="designer.ui.shortcutsOpen = true"><AppIcon name="keyboard" small />快捷键</button>
  </div>
</template>

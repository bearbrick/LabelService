<script setup lang="ts">
import { computed } from 'vue'
import { designer } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'

const cursor = computed(() => {
  const format = (value: number | null) => (value === null ? '—' : value.toFixed(1))
  return `X ${format(designer.ui.cursorX)} · Y ${format(designer.ui.cursorY)} mm`
})
const saved = computed(() => (designer.dirty ? '有修改，正在保存草稿…' : designer.savedAt ? `草稿已保存到本机 ${designer.savedAt}` : ''))
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

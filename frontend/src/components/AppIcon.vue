<script setup lang="ts">
import { computed } from 'vue'
import { UI_ICONS } from './uiIcons.ts'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 图标名（见 uiIcons.ts）。
   */
  name: string
  /**
   * 小号（12 像素）。
   */
  small?: boolean
}>()
/**
 * 图标的 SVG 内容；名字不存在时为空。
 */
const body = computed(() => UI_ICONS[props.name] ?? '')
</script>

<template>
  <svg class="svgi" :class="{ sm: small }" viewBox="0 0 16 16" aria-hidden="true" v-html="body"></svg>
</template>

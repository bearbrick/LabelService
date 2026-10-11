<script setup lang="ts">
/**
 * 颜色：取色器 + #RRGGBB 文本框。
 */

/**
 * 颜色值 #RRGGBB（v-model）；没设颜色时为 undefined。
 */
const model = defineModel<string | undefined>()
/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 颜色改了（父组件据此记录撤销）。
   */
  change: []
}>()
defineProps<{
  /**
   * 文本框的 id（label 的 for 用）。
   */
  id: string
  /**
   * 标签文字。
   */
  label: string
}>()

/**
 * 设置颜色：不是 #RRGGBB 时忽略，合法时转大写并通知父组件。
 * @param value 输入的颜色。
 */
function set(value: string): void {
  if (!/^#[0-9a-fA-F]{6}$/.test(value)) return
  model.value = value.toUpperCase()
  emit('change')
}
</script>

<template>
  <div class="color-row">
    <input :id="id" type="color" class="color-inp" :value="model ?? '#000000'" :aria-label="label" @change="set(($event.target as HTMLInputElement).value)" />
    <input class="inp mono" :value="model ?? '#000000'" :aria-label="`${label}（#RRGGBB）`" @change="set(($event.target as HTMLInputElement).value)" />
  </div>
</template>

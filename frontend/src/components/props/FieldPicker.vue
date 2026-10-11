<script setup lang="ts">
/**
 * 选一个字段（插入或绑定），选完自动复位。
 */
import { computed } from 'vue'
import type { FieldType } from '../../model/template.ts'
import { designer } from '../../stores/designer.ts'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 下拉框的提示文字。
   */
  placeholder: string
  /**
   * 只列这些类型的字段；不传时列全部。
   */
  types?: FieldType[]
}>()
/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 选中了一个字段，参数是字段 key。
   */
  pick: [key: string]
}>()
/**
 * 可选的字段。
 */
const fields = computed(() => designer.template.fields.filter((field) => !props.types || props.types.includes(field.type)))

/**
 * 选中后通知父组件，并把下拉框复位（同一个字段可以再选一次）。
 * @param event 下拉框的 change 事件。
 */
function onChange(event: Event): void {
  const select = event.target as HTMLSelectElement
  const key = select.value
  select.value = ''
  if (key) emit('pick', key)
}
</script>

<template>
  <select class="sel" :aria-label="placeholder" @change="onChange">
    <option value="">{{ placeholder }}</option>
    <option v-for="field in fields" :key="field.key" :value="field.key">{{ field.name || field.key }}（{{ field.key }}）</option>
  </select>
</template>

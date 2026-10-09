<script setup lang="ts">
/** 选一个字段（插入或绑定），选完自动复位。 */
import { computed } from 'vue'
import type { FieldType } from '../../model/template.ts'
import { designer } from '../../stores/designer.ts'

const props = defineProps<{ placeholder: string; types?: FieldType[] }>()
const emit = defineEmits<{ pick: [key: string] }>()
const fields = computed(() => designer.template.fields.filter((field) => !props.types || props.types.includes(field.type)))

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

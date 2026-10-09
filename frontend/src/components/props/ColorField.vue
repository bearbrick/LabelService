<script setup lang="ts">
/** 颜色：取色器 + #RRGGBB 文本框。 */
const model = defineModel<string | undefined>()
const emit = defineEmits<{ change: [] }>()
defineProps<{ id: string; label: string }>()

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

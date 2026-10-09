<script setup lang="ts">
import { computed } from 'vue'
import { UI_ICONS } from './uiIcons.ts'

const props = defineProps<{ name: string; small?: boolean }>()
const body = computed(() => UI_ICONS[props.name] ?? '')
</script>

<template>
  <svg class="svgi" :class="{ sm: small }" viewBox="0 0 16 16" aria-hidden="true" v-html="body"></svg>
</template>

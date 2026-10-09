<script setup lang="ts">
import { designer } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'
import ElementLibrary from './ElementLibrary.vue'
import FieldList from './FieldList.vue'
import LayerList from './LayerList.vue'

const TABS = [
  { id: 'elements', label: '元素', icon: 'shapes' },
  { id: 'fields', label: '字段', icon: 'braces' },
  { id: 'layers', label: '图层', icon: 'layers' },
] as const
</script>

<template>
  <aside class="panel" aria-label="元素、字段和图层">
    <div class="ptabs" role="tablist">
      <button
        v-for="tab in TABS"
        :key="tab.id"
        type="button"
        role="tab"
        :aria-selected="designer.ui.leftTab === tab.id"
        :class="{ on: designer.ui.leftTab === tab.id }"
        @click="designer.ui.leftTab = tab.id"
      >
        <AppIcon :name="tab.icon" small />{{ tab.label }}
      </button>
    </div>
    <ElementLibrary v-if="designer.ui.leftTab === 'elements'" />
    <FieldList v-else-if="designer.ui.leftTab === 'fields'" />
    <LayerList v-else />
  </aside>
</template>

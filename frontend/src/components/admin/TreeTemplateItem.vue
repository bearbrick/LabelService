<script setup lang="ts">
/**
 * 模板树里的一个模板：状态点、名称（最多两行），下面一行小字（编码和尺寸，或编码和分组）。最近打开、搜索结果、分组里都用它。
 */
import type { TemplateItem } from '../../api/admin.ts'
import { templateState } from '../../stores/catalog.ts'

defineProps<{
  /**
   * 模板。
   */
  template: TemplateItem
  /**
   * 名称下面的小字。
   */
  sub: string
  /**
   * 是不是当前打开的模板。
   */
  active: boolean
  /**
   * 不在分组下面（最近打开、搜索结果），左边不缩进。
   */
  flat?: boolean
}>()

defineEmits<{
  /**
   * 点了这个模板。
   */
  open: []
}>()
</script>

<template>
  <button class="table-item" type="button" :class="{ active, flat }" :aria-label="template.name + ' · ' + template.code" :title="`${template.name}\n${sub}`" @click="$emit('open')">
    <span><span class="state-dot" :class="templateState(template).key"></span><span class="tree-template-name">{{ template.name }}</span></span>
    <small>{{ sub }}</small>
  </button>
</template>

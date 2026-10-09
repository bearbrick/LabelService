<script setup lang="ts">
/** 图片和图标的属性。 */
import { computed } from 'vue'
import { LABEL_ICONS } from '../../model/icons.ts'
import type { IconElement, ImageElement, ImageFit } from '../../model/template.ts'
import { assets } from '../../stores/assets.ts'
import { commit, designer } from '../../stores/designer.ts'
import ColorField from './ColorField.vue'

const props = defineProps<{ element: ImageElement | IconElement }>()
const FITS: ReadonlyArray<{ value: ImageFit; label: string }> = [
  { value: 'contain', label: '适应' },
  { value: 'cover', label: '填满' },
  { value: 'stretch', label: '拉伸' },
]

const image = computed(() => (props.element.type === 'image' ? props.element : null))
const icon = computed(() => (props.element.type === 'icon' ? props.element : null))
const imageFields = computed(() => designer.template.fields.filter((field) => field.type === 'image'))

function setSource(value: string): void {
  if (image.value) image.value.src = value
  commit()
}

function setFit(value: ImageFit): void {
  if (image.value) image.value.fit = value
  commit()
}

function setIcon(key: string): void {
  if (icon.value) icon.value.icon = key
  commit()
}
</script>

<template>
  <template v-if="image">
    <h4>图片</h4>
    <div class="prow">
      <label for="prop-src">来源</label>
      <select id="prop-src" class="sel" :value="image.src" @change="setSource(($event.target as HTMLSelectElement).value)">
        <optgroup label="素材库">
          <option v-for="asset in assets" :key="asset.key" :value="`asset:${asset.key}`">{{ asset.name }}</option>
        </optgroup>
        <optgroup label="字段（image 类型）">
          <option v-for="field in imageFields" :key="field.key" :value="`{{${field.key}}}`">{{ field.name || field.key }}（{{ field.key }}）</option>
        </optgroup>
      </select>
    </div>
    <div class="prow">
      <span class="lb">填充</span>
      <div class="seg" role="group" aria-label="填充方式">
        <button v-for="item in FITS" :key="item.value" type="button" :class="{ on: (image.fit ?? 'contain') === item.value }" @click="setFit(item.value)">{{ item.label }}</button>
      </div>
    </div>
    <div class="prow"><span class="lb">黑白化</span><label class="check"><input v-model="image.mono" type="checkbox" @change="commit()" />按热敏打印效果显示</label></div>
  </template>

  <template v-if="icon">
    <h4>图标</h4>
    <div class="icon-grid">
      <div
        v-for="(item, key) in LABEL_ICONS"
        :key="key"
        class="icon-cell"
        :class="{ on: icon.icon === key }"
        :data-tip="item.name"
        :aria-label="item.name"
        role="button"
        tabindex="0"
        @click="setIcon(key)"
        @keydown.enter="setIcon(key)"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true"><path :d="item.d"></path></svg>
      </div>
    </div>
    <div class="prow" style="margin-top: 12px"><label for="prop-icon-color">颜色</label><ColorField id="prop-icon-color" v-model="icon.color" label="图标颜色" @change="commit()" /></div>
  </template>
</template>

<script setup lang="ts">
/**
 * 线和矩形的属性。线宽、虚线、圆角单位都是 mm。
 */
import { computed } from 'vue'
import { DEFAULT_DASH } from '../../model/elements.ts'
import { STROKE_DEFAULT_WIDTH } from '../../model/template.ts'
import type { LineElement, RectElement, StrokeStyle } from '../../model/template.ts'
import { commit, setLineDirection } from '../../stores/designer.ts'
import ColorField from './ColorField.vue'

const props = defineProps<{ element: LineElement | RectElement }>()
const rect = computed(() => (props.element.type === 'rect' ? props.element : null))
// 元素在新建和打开时已补全默认值（normalizeElement），这里的兜底只为类型安全。
const stroke = computed<StrokeStyle>(() => props.element.stroke ?? { width: STROKE_DEFAULT_WIDTH, color: '#000000' })

function setDash(on: boolean): void {
  stroke.value.dash = on ? [...DEFAULT_DASH] : undefined
  commit()
}

function setFill(on: boolean): void {
  if (rect.value) rect.value.fill = on ? '#000000' : undefined
  commit()
}
</script>

<template>
  <template v-if="element.type === 'line'">
    <h4>线条</h4>
    <div class="prow">
      <span class="lb">方向</span>
      <div class="seg" role="group" aria-label="线条方向">
        <button type="button" :class="{ on: element.height === 0 }" @click="setLineDirection(element, 'h')">横线</button>
        <button type="button" :class="{ on: element.width === 0 }" @click="setLineDirection(element, 'v')">竖线</button>
      </div>
    </div>
  </template>
  <h4 v-else>矩形</h4>

  <div class="pgrid">
    <div class="cell wide"><span>{{ rect ? '边框' : '粗细' }}</span><div class="unit" data-unit="mm"><input v-model.number="stroke.width" class="inp num" type="number" step="0.1" min="0" aria-label="线宽" @change="commit()" /></div></div>
    <div v-if="rect" class="cell wide"><span>圆角</span><div class="unit" data-unit="mm"><input v-model.number="rect.radius" class="inp num" type="number" step="0.5" min="0" aria-label="圆角" @change="commit()" /></div></div>
  </div>
  <div class="prow"><span class="lb">样式</span><label class="check"><input type="checkbox" :checked="!!stroke.dash" @change="setDash(($event.target as HTMLInputElement).checked)" />虚线</label></div>
  <div class="prow"><label for="prop-stroke-color">{{ rect ? '边框色' : '颜色' }}</label><ColorField id="prop-stroke-color" v-model="stroke.color" label="线条颜色" @change="commit()" /></div>
  <div v-if="rect" class="prow">
    <span class="lb">填充</span>
    <div style="display: flex; gap: 6px; align-items: center">
      <label class="check"><input type="checkbox" :checked="!!rect.fill" @change="setFill(($event.target as HTMLInputElement).checked)" />填充</label>
      <ColorField v-if="rect.fill" id="prop-fill" v-model="rect.fill" label="填充颜色" @change="commit()" />
    </div>
  </div>
</template>

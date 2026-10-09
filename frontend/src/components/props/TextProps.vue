<script setup lang="ts">
import { computed } from 'vue'
import { defaultTextStyle } from '../../model/elements.ts'
import { FONT_OPTIONS } from '../../model/fonts.ts'
import type { TextAlign, TextElement, TextStyle, VerticalAlign } from '../../model/template.ts'
import { commit } from '../../stores/designer.ts'
import AppIcon from '../AppIcon.vue'
import ColorField from './ColorField.vue'
import FieldPicker from './FieldPicker.vue'

const props = defineProps<{ element: TextElement }>()
// 元素在新建和打开时已补全默认值（normalizeElement），这里的兜底只为类型安全。
const style = computed(() => props.element.style ?? defaultTextStyle())

const H_ALIGNS: ReadonlyArray<{ value: TextAlign; icon: string; label: string }> = [
  { value: 'left', icon: 'alignTextL', label: '左对齐' },
  { value: 'center', icon: 'alignTextC', label: '居中' },
  { value: 'right', icon: 'alignTextR', label: '右对齐' },
]
const V_ALIGNS: ReadonlyArray<{ value: VerticalAlign; icon: string; label: string }> = [
  { value: 'top', icon: 'vTop', label: '顶部' },
  { value: 'middle', icon: 'vMid', label: '居中' },
  { value: 'bottom', icon: 'vBot', label: '底部' },
]

function set<K extends keyof TextStyle>(key: K, value: TextStyle[K]): void {
  style.value[key] = value
  commit()
}

function insert(key: string): void {
  props.element.content += `{{${key}}}`
  commit()
}
</script>

<template>
  <h4>文本</h4>
  <textarea id="prop-content" v-model="element.content" class="inp mono" rows="3" aria-label="文本内容" @change="commit()"></textarea>
  <div style="margin: 6px 0 12px"><FieldPicker placeholder="插入字段…" @pick="insert" /></div>
  <div class="prow">
    <label for="prop-font">字体</label>
    <select id="prop-font" v-model="style.fontFamily" class="sel" @change="commit()">
      <option v-for="font in FONT_OPTIONS" :key="font.value" :value="font.value">{{ font.label }}</option>
    </select>
  </div>
  <div class="prow">
    <label for="prop-size">字号</label>
    <div style="display: flex; gap: 4px; align-items: center">
      <div class="unit" data-unit="pt" style="flex: 1"><input id="prop-size" v-model.number="style.fontSize" class="inp num" type="number" step="0.5" min="3" max="200" @change="commit()" /></div>
      <button class="icon-btn" type="button" aria-label="加粗" :class="{ on: style.bold }" @click="set('bold', !style.bold)"><b>B</b></button>
      <button class="icon-btn" type="button" aria-label="斜体" :class="{ on: style.italic }" @click="set('italic', !style.italic)"><i style="font-family: serif">I</i></button>
      <button class="icon-btn" type="button" aria-label="下划线" :class="{ on: style.underline }" @click="set('underline', !style.underline)"><u>U</u></button>
    </div>
  </div>
  <div class="prow"><label for="prop-color">颜色</label><ColorField id="prop-color" v-model="style.color" label="文字颜色" @change="commit()" /></div>
  <div class="prow">
    <span class="lb">水平</span>
    <div class="seg" role="group" aria-label="水平对齐">
      <button v-for="item in H_ALIGNS" :key="item.value" type="button" :aria-label="item.label" :class="{ on: style.align === item.value }" @click="set('align', item.value)"><AppIcon :name="item.icon" small /></button>
    </div>
  </div>
  <div class="prow">
    <span class="lb">垂直</span>
    <div class="seg" role="group" aria-label="垂直对齐">
      <button v-for="item in V_ALIGNS" :key="item.value" type="button" :aria-label="item.label" :class="{ on: style.vAlign === item.value }" @click="set('vAlign', item.value)"><AppIcon :name="item.icon" small /></button>
    </div>
  </div>
  <div class="prow">
    <label for="prop-fit">放不下时</label>
    <select id="prop-fit" v-model="style.fit" class="sel" @change="commit()">
      <option value="none">单行，超出裁掉</option>
      <option value="wrap">自动换行</option>
      <option value="shrink">自动缩小字号</option>
    </select>
  </div>
  <div v-if="style.fit === 'shrink'" class="prow">
    <label for="prop-minsize">最小字号</label>
    <div class="unit" data-unit="pt"><input id="prop-minsize" v-model.number="style.minFontSize" class="inp num" type="number" step="0.5" min="3" @change="commit()" /></div>
  </div>
  <div class="pgrid">
    <div class="cell wide"><span>行高</span><input v-model.number="style.lineHeight" class="inp num" type="number" step="0.1" min="0.8" max="3" aria-label="行高" @change="commit()" /></div>
    <div class="cell wide"><span>字距</span><div class="unit" data-unit="mm"><input v-model.number="style.letterSpacing" class="inp num" type="number" step="0.1" min="0" aria-label="字距" @change="commit()" /></div></div>
  </div>
</template>

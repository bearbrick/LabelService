<script setup lang="ts">
/**
 * 一维码和二维码的属性。
 */
import { computed } from 'vue'
import { SYMBOLOGY_LABELS } from '../../editor/barcode.ts'
import type { BarcodeElement, ErrorCorrectionLevel, QrCodeElement, TextAlign } from '../../model/template.ts'
import { commit } from '../../stores/designer.ts'
import AppIcon from '../AppIcon.vue'
import FieldPicker from './FieldPicker.vue'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 一维码或二维码元素。
   */
  element: BarcodeElement | QrCodeElement
}>()
/**
 * 一维码可选的码制。
 */
const LINEAR = ['code128', 'code39', 'ean13', 'ean8', 'upca', 'itf14', 'gs1-128'] as const
/**
 * 二维码可选的码制。
 */
const MATRIX = ['qr', 'datamatrix', 'pdf417'] as const
/**
 * QR 纠错级别的按钮。
 */
const LEVELS: ErrorCorrectionLevel[] = ['L', 'M', 'Q', 'H']
/**
 * 一维码在框内水平对齐的按钮。
 */
const ALIGNS: ReadonlyArray<{ value: TextAlign; icon: string; label: string }> = [
  { value: 'left', icon: 'alignTextL', label: '左对齐' },
  { value: 'center', icon: 'alignTextC', label: '居中' },
  { value: 'right', icon: 'alignTextR', label: '右对齐' },
]

/**
 * 是一维码时是它，否则为 null。
 */
const barcode = computed(() => (props.element.type === 'barcode' ? props.element : null))
/**
 * 是二维码时是它，否则为 null。
 */
const matrix = computed(() => (props.element.type === 'qrcode' ? props.element : null))
/**
 * 码制下拉框的选项（按元素类型）。
 */
const options = computed(() => (props.element.type === 'barcode' ? LINEAR : MATRIX))

/**
 * 一维码：绑定替换整个内容；二维码：追加字段，用 | 分隔。
 * @param key 字段 key。
 */
function pickField(key: string): void {
  if (props.element.type === 'qrcode' && props.element.value) props.element.value += `|{{${key}}}`
  else props.element.value = `{{${key}}}`
  commit()
}

/**
 * 设置一维码在框内的水平对齐。
 * @param value 对齐方式。
 */
function setAlign(value: TextAlign): void {
  if (barcode.value) barcode.value.align = value
  commit()
}

/**
 * 设置 QR 纠错级别。
 * @param value 纠错级别。
 */
function setLevel(value: ErrorCorrectionLevel): void {
  if (matrix.value) matrix.value.ecLevel = value
  commit()
}
</script>

<template>
  <h4>{{ barcode ? '一维码' : '二维码' }}</h4>
  <div class="prow">
    <label for="prop-symbology">码制</label>
    <select id="prop-symbology" v-model="element.symbology" class="sel" @change="commit()">
      <option v-for="value in options" :key="value" :value="value">{{ SYMBOLOGY_LABELS[value] }}</option>
    </select>
  </div>
  <div class="prow" style="align-items: start">
    <label for="prop-value" style="padding-top: 6px">内容</label>
    <input v-if="barcode" id="prop-value" v-model="element.value" class="inp mono" @change="commit()" />
    <textarea v-else id="prop-value" v-model="element.value" class="inp mono" rows="3" @change="commit()"></textarea>
  </div>
  <div class="prow"><span class="lb"></span><FieldPicker :placeholder="barcode ? '绑定字段…' : '追加字段…'" :types="['string', 'number', 'date']" @pick="pickField" /></div>

  <template v-if="barcode">
    <div class="prow">
      <span class="lb">对齐</span>
      <div class="seg" role="group" aria-label="条码对齐">
        <button v-for="item in ALIGNS" :key="item.value" type="button" :aria-label="item.label" :class="{ on: (barcode.align ?? 'left') === item.value }" @click="setAlign(item.value)"><AppIcon :name="item.icon" small /></button>
      </div>
    </div>
    <div class="prow">
      <span class="lb">文字</span>
      <div style="display: flex; gap: 8px; align-items: center">
        <label class="check"><input v-model="barcode.showText" type="checkbox" @change="commit()" />显示</label>
        <select v-model="barcode.textPosition" class="sel" :disabled="!barcode.showText" aria-label="文字位置" @change="commit()">
          <option value="bottom">在下方</option>
          <option value="top">在上方</option>
        </select>
      </div>
    </div>
    <div v-if="barcode.showText && barcode.textStyle" class="prow">
      <label for="prop-text-size">文字字号</label>
      <div class="unit" data-unit="pt"><input id="prop-text-size" v-model.number="barcode.textStyle.fontSize" class="inp num" type="number" step="0.5" min="4" @change="commit()" /></div>
    </div>
    <div class="prow">
      <label for="prop-module">最小模块</label>
      <div class="unit" data-unit="点"><input id="prop-module" v-model.number="barcode.minModuleDots" class="inp num" type="number" step="1" min="1" max="8" @change="commit()" /></div>
    </div>
  </template>

  <div v-if="matrix && (matrix.symbology ?? 'qr') === 'qr'" class="prow">
    <span class="lb">纠错</span>
    <div class="seg" role="group" aria-label="纠错级别">
      <button v-for="level in LEVELS" :key="level" type="button" :class="{ on: (matrix.ecLevel ?? 'M') === level }" @click="setLevel(level)">{{ level }}</button>
    </div>
  </div>
  <div class="prow">
    <label for="prop-quiet">留白</label>
    <div class="unit" data-unit="mm"><input id="prop-quiet" v-model.number="element.quietZone" class="inp num" type="number" step="0.5" min="0" @change="commit()" /></div>
  </div>
</template>

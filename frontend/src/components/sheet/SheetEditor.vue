<script setup lang="ts">
/** 拼版设计器：工具栏、左侧模板和自动排列、画布、右侧属性，以及键盘快捷键。 */
import { onBeforeUnmount, onMounted, ref } from 'vue'
import {
  alignSelected,
  distributeSelected,
  duplicateSelected,
  nudge,
  redo,
  removeSelected,
  renumberSlots,
  rotateSelected,
  select,
  selectAll,
  sheet,
  undo,
} from '../../stores/sheet.ts'
import '../../sheet.css'
import AppIcon from '../AppIcon.vue'
import SheetCanvas from './SheetCanvas.vue'
import SheetProps from './SheetProps.vue'
import SheetSidebar from './SheetSidebar.vue'

const props = defineProps<{ readOnly: boolean }>()
const emit = defineEmits<{ save: [] }>()
const canvas = ref<InstanceType<typeof SheetCanvas> | null>(null)
const count = () => sheet.selection.length

function onKeyDown(event: KeyboardEvent): void {
  const target = event.target as HTMLElement | null
  const typing = !!target && (['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) || target.isContentEditable)
  const mod = event.ctrlKey || event.metaKey
  const key = event.key.toLowerCase()
  if (mod && key === 's') {
    event.preventDefault()
    emit('save')
    return
  }

  if (typing || props.readOnly) return
  const actions: Array<[boolean, () => void]> = [
    [mod && !event.shiftKey && key === 'z', undo],
    [mod && (key === 'y' || (event.shiftKey && key === 'z')), redo],
    [mod && key === 'a', selectAll],
    [mod && key === 'd', duplicateSelected],
    [!mod && key === 'r', rotateSelected],
    [event.key === 'Delete' || event.key === 'Backspace', removeSelected],
    [event.key === 'Escape', () => select([])],
  ]
  const hit = actions.find(([matches]) => matches)
  if (hit) {
    event.preventDefault()
    hit[1]()
    return
  }

  const step = event.shiftKey ? 1 : 0.1
  const arrows: Record<string, [number, number]> = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] }
  if (arrows[event.key] && count()) {
    event.preventDefault()
    nudge(...arrows[event.key])
  }
}

onMounted(() => window.addEventListener('keydown', onKeyDown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeyDown))
</script>

<template>
  <div class="designer sheet-designer">
    <div class="toolbar" role="toolbar" aria-label="拼版工具">
      <fieldset class="fs tb-group" :disabled="props.readOnly">
        <button class="icon-btn" type="button" data-tip="撤销 Ctrl+Z" aria-label="撤销" :disabled="!sheet.canUndo" @click="undo()"><AppIcon name="undo" /></button>
        <button class="icon-btn" type="button" data-tip="重做 Ctrl+Y" aria-label="重做" :disabled="!sheet.canRedo" @click="redo()"><AppIcon name="redo" /></button>
        <span class="tb-sep"></span>
        <button class="icon-btn" type="button" data-tip="左对齐（一个时对齐纸边）" aria-label="左对齐" :disabled="!count()" @click="alignSelected('left')"><AppIcon name="alignL" /></button>
        <button class="icon-btn" type="button" data-tip="水平居中" aria-label="水平居中" :disabled="!count()" @click="alignSelected('hcenter')"><AppIcon name="alignCH" /></button>
        <button class="icon-btn" type="button" data-tip="右对齐" aria-label="右对齐" :disabled="!count()" @click="alignSelected('right')"><AppIcon name="alignR" /></button>
        <button class="icon-btn" type="button" data-tip="顶对齐" aria-label="顶对齐" :disabled="!count()" @click="alignSelected('top')"><AppIcon name="alignT" /></button>
        <button class="icon-btn" type="button" data-tip="垂直居中" aria-label="垂直居中" :disabled="!count()" @click="alignSelected('vcenter')"><AppIcon name="alignCV" /></button>
        <button class="icon-btn" type="button" data-tip="底对齐" aria-label="底对齐" :disabled="!count()" @click="alignSelected('bottom')"><AppIcon name="alignB" /></button>
        <button class="icon-btn" type="button" data-tip="水平等距（至少 3 个）" aria-label="水平等距" :disabled="count() < 3" @click="distributeSelected('h')"><AppIcon name="distH" /></button>
        <button class="icon-btn" type="button" data-tip="垂直等距（至少 3 个）" aria-label="垂直等距" :disabled="count() < 3" @click="distributeSelected('v')"><AppIcon name="distV" /></button>
        <span class="tb-sep"></span>
        <button class="icon-btn" type="button" data-tip="旋转 90° R" aria-label="旋转 90 度" :disabled="!count()" @click="rotateSelected()"><AppIcon name="refresh" /></button>
        <button class="icon-btn" type="button" data-tip="复制一份 Ctrl+D" aria-label="复制一份" :disabled="!count()" @click="duplicateSelected()"><AppIcon name="copy" /></button>
        <button class="icon-btn" type="button" data-tip="删除 Delete" aria-label="删除" :disabled="!count()" @click="removeSelected()"><AppIcon name="trash" /></button>
        <span class="tb-sep"></span>
        <button class="btn sm" type="button" data-tip="按位置重新编号：一行一行从左到右" @click="renumberSlots('rows')">先行后列</button>
        <button class="btn sm" type="button" data-tip="按位置重新编号：一列一列从上到下" @click="renumberSlots('cols')">先列后行</button>
      </fieldset>
      <span class="spacer"></span>
      <button class="icon-btn" type="button" data-tip="显示网格" aria-label="显示网格" :class="{ on: sheet.settings.showGrid }" @click="sheet.settings.showGrid = !sheet.settings.showGrid"><AppIcon name="grid" /></button>
      <button class="icon-btn" type="button" data-tip="吸附：对齐参考线和网格" aria-label="吸附" :class="{ on: sheet.settings.snap }" @click="sheet.settings.snap = !sheet.settings.snap"><AppIcon name="magnet" /></button>
      <select v-model.number="sheet.settings.grid" class="sel sm" aria-label="网格间距" style="width: 84px">
        <option v-for="size in [0.5, 1, 2, 5, 10]" :key="size" :value="size">网格 {{ size }}</option>
      </select>
      <span class="tb-sep"></span>
      <button class="icon-btn" type="button" data-tip="缩小（Ctrl+滚轮）" aria-label="缩小" @click="canvas?.zoomBy(1 / 1.25)"><AppIcon name="zoomOut" /></button>
      <button class="icon-btn" type="button" data-tip="整张纸" aria-label="适合窗口" @click="canvas?.fit()"><AppIcon name="fit" /></button>
      <button class="icon-btn" type="button" data-tip="放大" aria-label="放大" @click="canvas?.zoomBy(1.25)"><AppIcon name="zoomIn" /></button>
    </div>
    <SheetSidebar :read-only="props.readOnly" />
    <SheetCanvas ref="canvas" />
    <SheetProps :read-only="props.readOnly" />
    <div class="statusbar sheet-status">
      <span>{{ sheet.layout?.slots.length ?? 0 }} 个格子</span>
      <span v-if="count()">已选 {{ count() }} 个</span>
      <span class="spacer"></span>
      <span class="muted">拖动格子吸附到其他格子和纸边；方向键微调 0.1 mm，Shift 加方向键 1 mm；Ctrl+滚轮缩放</span>
    </div>
    <div class="narrow-note">
      <div class="in">
        <b>窗口太窄，放不下拼版设计器</b>
        <span>请把窗口加宽到 1100px 以上。</span>
      </div>
    </div>
  </div>
</template>

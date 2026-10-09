<script setup lang="ts">
/**
 * 设计画布：模板 JSON 就是画布状态，按元素数组顺序叠放。
 * 目前能显示全部元素（文本显示原文，条码、二维码、图片、图标先显示占位框），能选中和拖动。
 * 缩放控制点、吸附、对齐线、撤销重做等交互见 docs/tasks/T-013。
 */
import { computed } from 'vue'
import type Konva from 'konva'
import { ELEMENT_LABELS, STROKE_DEFAULT_WIDTH, TEXT_DEFAULTS } from '../model/template'
import type { LabelElement, LabelTemplate, StrokeStyle } from '../model/template'
import { pointsToMm, roundMm } from '../model/units'

const props = defineProps<{
  template: LabelTemplate
  /** 每毫米多少屏幕像素。 */
  scale: number
  selectedId: string | null
}>()

const emit = defineEmits<{
  select: [id: string | null]
  move: [id: string, x: number, y: number]
}>()

const MARGIN = 24
const SELECTION = '#2F6FE4'

const px = (mm: number) => mm * props.scale

const stageConfig = computed(() => ({
  width: px(props.template.page.width) + MARGIN * 2,
  height: px(props.template.page.height) + MARGIN * 2,
}))

const pageConfig = computed(() => ({
  x: MARGIN,
  y: MARGIN,
  width: px(props.template.page.width),
  height: px(props.template.page.height),
  fill: '#FFFFFF',
  stroke: '#C9D1DC',
  strokeWidth: 1,
  shadowColor: 'rgba(15, 23, 42, 0.12)',
  shadowBlur: 8,
  shadowOffsetY: 2,
}))

/** 元素组以中心为原点旋转，和服务端“绕元素中心顺时针旋转”一致。 */
function groupConfig(element: LabelElement) {
  return {
    id: element.id,
    x: MARGIN + px(element.x + element.width / 2),
    y: MARGIN + px(element.y + element.height / 2),
    offsetX: px(element.width) / 2,
    offsetY: px(element.height) / 2,
    rotation: element.rotation ?? 0,
    draggable: !element.locked,
    visible: !element.hidden,
  }
}

function strokeConfig(stroke: StrokeStyle | undefined) {
  return {
    stroke: stroke?.color ?? '#000000',
    strokeWidth: px(stroke?.width ?? STROKE_DEFAULT_WIDTH),
    dash: stroke?.dash?.map(px),
  }
}

function rectConfig(element: LabelElement) {
  if (element.type !== 'rect') return {}
  const width = element.stroke ? px(element.stroke.width ?? STROKE_DEFAULT_WIDTH) : 0
  // 边框画在框内侧，和服务端 RectPainter 一致。
  return {
    x: width / 2,
    y: width / 2,
    width: px(element.width) - width,
    height: px(element.height) - width,
    fill: element.fill,
    cornerRadius: px(element.radius ?? 0),
    ...(element.stroke ? strokeConfig(element.stroke) : { strokeEnabled: false }),
  }
}

function lineConfig(element: LabelElement) {
  if (element.type !== 'line') return {}
  return { points: [0, 0, px(element.width), px(element.height)], ...strokeConfig(element.stroke) }
}

function textConfig(element: LabelElement) {
  if (element.type !== 'text') return {}
  const style = { ...TEXT_DEFAULTS, ...element.style }
  return {
    text: element.content,
    width: px(element.width),
    height: px(element.height),
    fontFamily: style.fontFamily,
    fontSize: px(pointsToMm(style.fontSize)),
    fontStyle: [style.bold ? 'bold' : '', style.italic ? 'italic' : ''].join(' ').trim() || 'normal',
    fill: style.color,
    align: style.align,
    verticalAlign: style.vAlign,
    lineHeight: style.lineHeight,
    wrap: style.fit === 'wrap' ? 'char' : 'none',
  }
}

function placeholderConfig(element: LabelElement) {
  return {
    width: px(element.width),
    height: px(element.height),
    fill: 'rgba(47, 111, 228, 0.06)',
    stroke: '#8AA8E8',
    strokeWidth: 1,
    dash: [4, 3],
  }
}

function placeholderTextConfig(element: LabelElement) {
  return {
    width: px(element.width),
    height: px(element.height),
    text: ELEMENT_LABELS[element.type],
    align: 'center',
    verticalAlign: 'middle',
    fontSize: 12,
    fill: '#4A6FB5',
  }
}

function selectionConfig(element: LabelElement) {
  return {
    width: Math.max(px(element.width), 1),
    height: Math.max(px(element.height), 1),
    stroke: SELECTION,
    strokeWidth: 1.5,
    dash: [5, 3],
    listening: false,
  }
}

function onDragEnd(event: Konva.KonvaEventObject<DragEvent>, element: LabelElement) {
  const node = event.target
  const x = roundMm((node.x() - MARGIN) / props.scale - element.width / 2)
  const y = roundMm((node.y() - MARGIN) / props.scale - element.height / 2)
  emit('move', element.id, x, y)
}

function onStageClick(event: Konva.KonvaEventObject<MouseEvent>) {
  if (event.target === event.target.getStage()) emit('select', null)
}
</script>

<template>
  <v-stage :config="stageConfig" @click="onStageClick">
    <v-layer>
      <v-rect :config="pageConfig" @click="emit('select', null)" />
      <v-group
        v-for="element in template.elements"
        :key="element.id"
        :config="groupConfig(element)"
        @click="emit('select', element.id)"
        @dragstart="emit('select', element.id)"
        @dragend="onDragEnd($event, element)"
      >
        <v-rect v-if="element.type === 'rect'" :config="rectConfig(element)" />
        <v-line v-else-if="element.type === 'line'" :config="lineConfig(element)" />
        <v-text v-else-if="element.type === 'text'" :config="textConfig(element)" />
        <template v-else>
          <v-rect :config="placeholderConfig(element)" />
          <v-text :config="placeholderTextConfig(element)" />
        </template>
        <v-rect v-if="element.id === selectedId" :config="selectionConfig(element)" />
      </v-group>
    </v-layer>
  </v-stage>
</template>

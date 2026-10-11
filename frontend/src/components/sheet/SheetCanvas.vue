<script setup lang="ts">
/**
 * 拼版画布：把 editor/sheetStage.ts 接到 stores/sheet.ts，版式、选择、预览图、设置变了就重画。
 */
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { SheetStage } from '../../editor/sheetStage.ts'
import { addSlotAt, moveSlots, problems, select, sheet } from '../../stores/sheet.ts'

/**
 * 画布的外层容器。
 */
const wrap = ref<HTMLElement | null>(null)

/**
 * Konva 的容器。
 */
const container = ref<HTMLDivElement | null>(null)

/**
 * 当前缩放比例（右下角显示）。
 */
const zoom = ref(1)

/**
 * 拼版画布；挂载后创建。
 */
let stage: SheetStage | null = null

/**
 * 监听画布尺寸：窗口变化后画布可能重新适应了整张纸，同步右下角的比例。
 */
let resizeObserver: ResizeObserver | null = null

onMounted(() => {
  stage = new SheetStage(wrap.value!, container.value!, {
    layout: () => sheet.layout,
    selection: () => sheet.selection,
    select,
    bad: () => problems.value.badSlots,
    image: () => sheet.previewImage,
    settings: () => sheet.settings,
    moved: moveSlots,
    dropped: addSlotAt,
  })
  stage.render()
  zoom.value = stage.zoom
  // 画布自己的尺寸监听先执行，下一帧再读比例
  resizeObserver = new ResizeObserver(() => requestAnimationFrame(syncZoom))
  resizeObserver.observe(wrap.value!)
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  stage?.destroy()
})

watch(
  () => [sheet.layout, sheet.selection, sheet.previewImage, sheet.settings, problems.value],
  () => {
    stage?.render()
    if (stage) zoom.value = stage.zoom
  },
  { deep: true },
)

/**
 * 按倍数缩放（工具栏按钮）。
 * @param factor 缩放倍数。
 */
function zoomBy(factor: number): void {
  stage?.zoomTo(stage.zoom * factor)
  zoom.value = stage?.zoom ?? 1
}

/**
 * 整张纸放进可见区域。
 */
function fit(): void {
  stage?.fit()
  zoom.value = stage?.zoom ?? 1
}

/**
 * 画布里 Ctrl+滚轮缩放后同步右下角的比例。
 */
function syncZoom(): void {
  zoom.value = stage?.zoom ?? zoom.value
}

defineExpose({ zoomBy, fit })
</script>

<template>
  <div ref="wrap" class="sheet-canvas" aria-label="拼版画布" @wheel.passive="syncZoom">
    <div ref="container" class="sheet-stage"></div>
    <div class="sheet-zoom">{{ Math.round(zoom * 100) }}%</div>
  </div>
</template>

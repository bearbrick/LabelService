<script setup lang="ts">
/**
 * 画布区域：标尺、Konva 画布、从左侧拖入元素和字段、双击直接编辑文本（输入 {{ 弹出字段补全）。
 * 直接编辑文本在 composables/useTextEdit.ts，拖放在 composables/useCanvasDrop.ts，这里负责画布、标尺和生命周期。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useCanvasDrop } from '../composables/useCanvasDrop.ts'
import { useTextEdit } from '../composables/useTextEdit.ts'
import { paintRuler } from '../editor/rulers.ts'
import { snapshotPage } from '../editor/snapshot.ts'
import { DesignerStage } from '../editor/stage.ts'
import { SERVER_FONT_FAMILIES } from '../model/fonts.ts'
import { assetSource, assetVersion } from '../stores/assets.ts'
import { commit, designer, registerCanvas, select, setMetric, toast } from '../stores/designer.ts'
import { endDrag } from '../stores/drag.ts'

/**
 * 画布的外层容器（尺寸、焦点、拖放）。
 */
const wrap = ref<HTMLDivElement>()

/**
 * Konva 的容器。
 */
const host = ref<HTMLDivElement>()

/**
 * 上方的横向标尺。
 */
const rulerTop = ref<HTMLCanvasElement>()

/**
 * 左侧的竖向标尺。
 */
const rulerLeft = ref<HTMLCanvasElement>()

/**
 * 直接编辑文本的输入框。
 */
const editor = ref<HTMLTextAreaElement>()

/**
 * 字体加载完一批就加一，让画布按新字体重画。
 */
const fontVersion = ref(0)

/**
 * 设计画布；挂载后创建，卸载时销毁。
 */
let stage: DesignerStage | null = null

/**
 * 监听容器尺寸变化。
 */
let resizeObserver: ResizeObserver | null = null

/**
 * 已经安排的标尺重画（requestAnimationFrame 的编号）；0 表示没有。
 */
let rulerFrame = 0

/**
 * 直接编辑文本的状态和事件处理。
 */
const { edit, suggestStyle, positionEditor, startEdit, onEditInput, pickSuggestion, onEditKey, onEditBlur } = useTextEdit(() => stage, editor, wrap)

/**
 * 拖放的事件处理。
 */
const { onDragOver, onDragLeave, onDrop, clearDrop } = useCanvasDrop(() => stage, wrap)

/**
 * 画布上方的提示：拖放时的说明，或空模板时的引导。
 */
const tip = computed(() => designer.ui.tip || (designer.template.elements.length ? '' : '从左侧拖入元素开始设计，或点击元素快速添加'))

/**
 * 影响全部元素绘制的上下文签名：显示方式、DPI、字段、字体、素材、异步资源。变了就全部重画。
 * @returns 签名文本。
 */
function contextSignature(): string {
  const page = designer.template.page
  return `${designer.ui.mode}|${page.dpi ?? 300}|${JSON.stringify(designer.template.fields)}|${fontVersion.value}|${assetVersion.value}|${designer.renderVersion}`
}

/**
 * 在下一帧重画两条标尺（同一帧里多次调用只画一次），颜色取当前主题的 CSS 变量。
 */
function paintRulers(): void {
  if (rulerFrame) return
  rulerFrame = requestAnimationFrame(() => {
    rulerFrame = 0
    if (!stage || !rulerTop.value || !rulerLeft.value) return
    const style = getComputedStyle(document.documentElement)
    const color = (name: string, fallback: string) => style.getPropertyValue(name).trim() || fallback
    const colors = {
      background: color('--sidebar', '#f8faff'),
      page: '#ffffff',
      highlight: color('--blue-soft', '#eaf2ff'),
      tick: color('--muted', '#7b8497'),
      label: color('--ink-2', '#566174'),
      cursor: color('--blue', '#1555d4'),
    }
    // 标尺要的视图状态：纸张原点、缩放、纸张尺寸、选中元素的范围、光标位置。
    const view = {
      originX: stage.origin.x,
      originY: stage.origin.y,
      scale: stage.scale,
      pageWidth: designer.template.page.width,
      pageHeight: designer.template.page.height,
      highlight: stage.selectionScreenBox(),
      cursorX: designer.ui.cursorX,
      cursorY: designer.ui.cursorY,
    }
    paintRuler(rulerTop.value, true, view, colors)
    paintRuler(rulerLeft.value, false, view, colors)
  })
}

/* ---------------- 生命周期 ---------------- */

onMounted(() => {
  if (!wrap.value || !host.value) return
  stage = new DesignerStage(wrap.value, host.value, {
    template: () => designer.template,
    selection: () => designer.selection,
    select,
    settings: () => ({ showGrid: designer.ui.showGrid, gridSize: designer.ui.gridSize, snap: designer.ui.snap, panMode: designer.ui.spaceDown }),
    drawContext: () => ({
      mode: designer.ui.mode,
      fields: designer.template.fields,
      dpi: designer.template.page.dpi ?? 300,
      assetSource,
      invalidate: () => designer.renderVersion++,
      report: setMetric,
    }),
    contextSignature,
    committed: commit,
    pointer: (x, y) => {
      designer.ui.cursorX = x
      designer.ui.cursorY = y
      paintRulers()
    },
    viewChanged: () => {
      if (!stage) return
      designer.ui.zoom = stage.zoom
      paintRulers()
      if (edit.id) positionEditor()
    },
    doubleClicked: (element) => {
      if (element.type === 'text') startEdit(element.id)
      else if (element.type === 'barcode' || element.type === 'qrcode') document.getElementById('prop-value')?.focus()
    },
    notify: (message) => toast(message),
  })
  registerCanvas({
    viewCenter: () => stage!.viewCenter(),
    zoomBy: (factor) => stage?.zoomBy(factor),
    zoomTo: (zoom) => stage?.zoomTo(zoom),
    zoomFit: () => stage?.zoomFit(),
    requestFit: () => stage?.requestFit(),
    editText: startEdit,
    snapshot: (width, height) => (host.value ? snapshotPage(host.value, designer.template.page, width, height) : null),
  })
  stage.sync()
  stage.resize()
  resizeObserver = new ResizeObserver(() => {
    stage?.resize()
    paintRulers()
  })
  resizeObserver.observe(wrap.value)
  document.fonts?.addEventListener?.('loadingdone', onFontsLoaded)
  // 提前加载服务端字体的常规和粗体，加载完会触发 loadingdone 重画。
  for (const family of SERVER_FONT_FAMILIES) for (const weight of [400, 700]) void document.fonts?.load(`${weight} 16px "${family}"`).catch(() => undefined)
  document.addEventListener('dragend', onGlobalDragEnd)
})

onBeforeUnmount(() => {
  registerCanvas(null)
  resizeObserver?.disconnect()
  document.fonts?.removeEventListener?.('loadingdone', onFontsLoaded)
  document.removeEventListener('dragend', onGlobalDragEnd)
  stage?.destroy()
  stage = null
})

/**
 * 一批字体加载完：画布按新字体重画（服务端字体下载完之前用的是本机字体）。
 */
function onFontsLoaded(): void {
  fontVersion.value++
}

/**
 * 拖动在别处结束或取消时，清掉拖放状态和提示。
 */
function onGlobalDragEnd(): void {
  endDrag()
  clearDrop()
}

watch(() => [designer.template, designer.ui.mode, designer.renderVersion, assetVersion.value, fontVersion.value], () => stage?.sync(), { deep: true, flush: 'post' })
watch(() => designer.selection.join(','), () => {
  stage?.refreshSelection()
  paintRulers()
}, { flush: 'post' })
watch(() => [designer.ui.showGrid, designer.ui.gridSize], () => stage?.stage.batchDraw())
watch(() => designer.ui.spaceDown, () => stage?.sync())
</script>

<template>
  <div class="workspace">
    <div class="ruler-corner" aria-hidden="true">mm</div>
    <canvas ref="rulerTop" class="ruler top" aria-hidden="true"></canvas>
    <canvas ref="rulerLeft" class="ruler left" aria-hidden="true"></canvas>
    <div
      ref="wrap"
      class="stage-wrap"
      tabindex="0"
      aria-label="标签画布"
      :class="{ space: designer.ui.spaceDown, drop: designer.ui.dropActive }"
      @dragover="onDragOver"
      @dragleave="onDragLeave"
      @drop="onDrop"
    >
      <div ref="host" class="stage-host"></div>
      <textarea
        v-if="edit.id"
        ref="editor"
        v-model="edit.value"
        class="text-editor"
        :style="edit.style"
        spellcheck="false"
        aria-label="编辑文本"
        @input="onEditInput"
        @keydown="onEditKey"
        @blur="onEditBlur"
      ></textarea>
      <div v-if="edit.id && edit.suggestions.length" class="suggest" :style="suggestStyle" role="listbox">
        <div
          v-for="(field, index) in edit.suggestions"
          :key="field.key"
          :class="{ on: index === edit.index }"
          role="option"
          @mousedown.prevent="pickSuggestion(field)"
        >
          <span class="k">{{ field.key }}</span><span class="muted">{{ field.name }}</span>
        </div>
      </div>
      <div v-if="tip" class="canvas-tip">{{ tip }}</div>
    </div>
  </div>
</template>

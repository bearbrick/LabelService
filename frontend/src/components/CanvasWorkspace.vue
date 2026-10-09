<script setup lang="ts">
/**
 * 画布区域：标尺、Konva 画布、从左侧拖入元素和字段、双击直接编辑文本（输入 {{ 弹出字段补全）。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { elementBounds } from '../editor/geometry.ts'
import { paintRuler } from '../editor/rulers.ts'
import { DesignerStage } from '../editor/stage.ts'
import { defaultTextStyle } from '../model/elements.ts'
import { cssFontFamily } from '../model/fonts.ts'
import type { FieldDefinition, LabelElement } from '../model/template.ts'
import { MM_PER_PT } from '../model/units.ts'
import { assetSource, assetVersion, uploadAsset } from '../stores/assets.ts'
import { addElement, commit, designer, registerCanvas, select, setMetric, toast } from '../stores/designer.ts'
import { drag, endDrag } from '../stores/drag.ts'

const wrap = ref<HTMLDivElement>()
const host = ref<HTMLDivElement>()
const rulerTop = ref<HTMLCanvasElement>()
const rulerLeft = ref<HTMLCanvasElement>()
const editor = ref<HTMLTextAreaElement>()
const fontVersion = ref(0)
let stage: DesignerStage | null = null
let resizeObserver: ResizeObserver | null = null
let rulerFrame = 0

const edit = reactive({
  id: null as string | null,
  value: '',
  style: {} as Record<string, string>,
  suggestions: [] as FieldDefinition[],
  index: 0,
  match: null as { start: number; end: number } | null,
})

const tip = computed(() => designer.ui.tip || (designer.template.elements.length ? '' : '从左侧拖入元素开始设计，或点击元素快速添加'))
const suggestStyle = computed(() => ({ left: edit.style.left, top: `${parseFloat(edit.style.top ?? '0') + parseFloat(edit.style.height ?? '0') + 4}px` }))

function find(id: string): LabelElement | undefined {
  return designer.template.elements.find((element) => element.id === id)
}

function contextSignature(): string {
  const page = designer.template.page
  return `${designer.ui.mode}|${page.dpi ?? 300}|${JSON.stringify(designer.template.fields)}|${fontVersion.value}|${assetVersion.value}|${designer.renderVersion}`
}

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

/* ---------------- 直接编辑文本 ---------------- */

function positionEditor(): void {
  const element = edit.id ? find(edit.id) : undefined
  if (!element || element.type !== 'text' || !stage) return
  const style = { ...defaultTextStyle(), ...element.style }
  const scale = stage.scale
  const fontSize = style.fontSize * MM_PER_PT * scale
  const point = stage.toScreen(element.x, element.y)
  edit.style = {
    left: `${point.x}px`,
    top: `${point.y}px`,
    width: `${Math.max(element.width * scale, 60)}px`,
    height: `${Math.max(element.height * scale, fontSize * 1.5)}px`,
    fontSize: `${fontSize}px`,
    fontFamily: cssFontFamily(style.fontFamily),
    fontWeight: style.bold ? '700' : '400',
    fontStyle: style.italic ? 'italic' : 'normal',
    lineHeight: String(style.lineHeight),
    textAlign: style.align,
    letterSpacing: `${(style.letterSpacing ?? 0) * scale}px`,
    transform: `rotate(${element.rotation ?? 0}deg)`,
  }
}

function startEdit(id: string): void {
  const element = find(id)
  if (!element || element.type !== 'text' || element.locked || !stage) return
  select([id])
  edit.id = id
  edit.value = element.content
  edit.suggestions = []
  positionEditor()
  stage.setEditing(id)
  nextTick(() => {
    editor.value?.focus()
    editor.value?.select()
  })
}

function finishEdit(save: boolean): void {
  if (!edit.id) return
  const element = find(edit.id)
  edit.id = null
  edit.suggestions = []
  if (element?.type === 'text' && save && edit.value !== element.content) {
    element.content = edit.value
    commit()
  }

  stage?.setEditing(null)
  wrap.value?.focus({ preventScroll: true })
}

function onEditInput(): void {
  const textarea = editor.value
  if (!textarea) return
  const before = textarea.value.slice(0, textarea.selectionStart)
  const match = /\{\{\s*([A-Za-z_]\w*)?$/.exec(before)
  if (!match) {
    edit.suggestions = []
    return
  }

  const query = (match[1] ?? '').toLowerCase()
  edit.suggestions = designer.template.fields
    .filter((field) => field.key.toLowerCase().startsWith(query) || (field.name ?? '').includes(match[1] ?? ''))
    .slice(0, 8)
  edit.index = 0
  edit.match = { start: match.index, end: textarea.selectionStart }
}

function pickSuggestion(field: FieldDefinition): void {
  const match = edit.match
  if (!match) return
  const insert = `{{${field.key}}}`
  edit.value = edit.value.slice(0, match.start) + insert + edit.value.slice(match.end)
  edit.suggestions = []
  nextTick(() => {
    const position = match.start + insert.length
    editor.value?.focus()
    editor.value?.setSelectionRange(position, position)
  })
}

function onEditKey(event: KeyboardEvent): void {
  event.stopPropagation()
  if (edit.suggestions.length) {
    const count = edit.suggestions.length
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      edit.index = (edit.index + (event.key === 'ArrowDown' ? 1 : count - 1)) % count
      return
    }

    if (event.key === 'Enter' || event.key === 'Tab') {
      event.preventDefault()
      pickSuggestion(edit.suggestions[edit.index])
      return
    }

    if (event.key === 'Escape') {
      event.preventDefault()
      edit.suggestions = []
      return
    }
  }

  if (event.key === 'Escape') {
    event.preventDefault()
    finishEdit(false)
  } else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    finishEdit(true)
  }
}

function onEditBlur(): void {
  setTimeout(() => {
    if (edit.id && document.activeElement !== editor.value) finishEdit(true)
  }, 0)
}

/* ---------------- 从左侧拖入 ---------------- */

function bindable(element: LabelElement | undefined, field: FieldDefinition | undefined): element is LabelElement {
  if (!element || !field || element.locked) return false
  if (element.type === 'barcode' || element.type === 'qrcode') return field.type !== 'image'
  return element.type === 'image' && field.type === 'image'
}

function onDragOver(event: DragEvent): void {
  const payload = drag.payload
  const files = !!event.dataTransfer?.types.includes('Files')
  if (!stage || (!payload && !files)) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy'
  designer.ui.dropActive = true
  stage.highlight(null)
  if (payload?.kind === 'field') {
    const field = designer.template.fields.find((item) => item.key === payload.key)
    const id = stage.elementAt(event)
    const target = id ? find(id) : undefined
    if (bindable(target, field)) {
      stage.highlight(elementBounds(target))
      designer.ui.tip = `松开把 ${payload.key} 绑定到“${target.name}”`
    } else {
      designer.ui.tip = `松开生成“${field?.name || payload.key}：{{${payload.key}}}”文本`
    }
  } else {
    designer.ui.tip = files ? '松开上传图片并放到这里' : '松开放置元素'
  }
}

function clearDrop(): void {
  designer.ui.dropActive = false
  designer.ui.tip = ''
  stage?.highlight(null)
}

function onDragLeave(event: DragEvent): void {
  if (!wrap.value?.contains(event.relatedTarget as Node | null)) clearDrop()
}

async function onDrop(event: DragEvent): Promise<void> {
  event.preventDefault()
  if (!stage) return
  const point = stage.pointerMm(event) ?? undefined
  const payload = drag.payload
  const target = payload?.kind === 'field' ? stage.elementAt(event) : null
  endDrag()
  clearDrop()
  const file = event.dataTransfer?.files?.[0]
  if (!payload && file) {
    try {
      const asset = await uploadAsset(file)
      addElement('image', { src: `asset:${asset.id}`, name: asset.name }, point)
    } catch (error) {
      toast((error as Error).message, true)
    }

    return
  }

  if (!payload) return
  if (payload.kind === 'element') addElement(payload.type, {}, point)
  else if (payload.kind === 'icon') addElement('icon', { icon: payload.icon, name: payload.name }, point)
  else if (payload.kind === 'asset') addElement('image', { src: `asset:${payload.id}`, name: payload.name }, point)
  else dropField(payload.key, target ? find(target) : undefined, point)
}

function dropField(key: string, target: LabelElement | undefined, point?: { x: number; y: number }): void {
  const field = designer.template.fields.find((item) => item.key === key)
  if (!field) return
  if (bindable(target, field)) {
    if (target.type === 'image') target.src = `{{${key}}}`
    else if (target.type === 'barcode' || target.type === 'qrcode') target.value = `{{${key}}}`
    select([target.id])
    commit()
    toast(`已把 ${key} 绑定到“${target.name}”`)
  } else if (field.type === 'image') {
    addElement('image', { src: `{{${key}}}`, name: field.name || key, width: 20, height: 20 }, point)
  } else {
    const label = field.name || key
    addElement('text', { content: `${label}：{{${key}}}`, name: label, width: 40, height: 5, style: { ...defaultTextStyle(), fontSize: 9, fit: 'shrink' } }, point)
  }
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
  })
  stage.sync()
  stage.resize()
  resizeObserver = new ResizeObserver(() => {
    stage?.resize()
    paintRulers()
  })
  resizeObserver.observe(wrap.value)
  document.fonts?.addEventListener?.('loadingdone', onFontsLoaded)
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

function onFontsLoaded(): void {
  fontVersion.value++
}

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

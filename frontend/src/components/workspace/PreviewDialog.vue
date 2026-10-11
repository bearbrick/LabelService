<script setup lang="ts">
/**
 * 服务端预览：把当前（未保存也行）的模板和样例值发给服务端渲染成 PNG，算法和开放接口一致。
 * 在设计页打开时可以和画布对比：叠加显示，并逐个元素量位置偏差（验收要求 ≤ 0.2 mm）。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive } from 'vue'
import { previewTemplate } from '../../api/admin.ts'
import { errorText } from '../../api/http.ts'
import { compareElements } from '../../editor/compare.ts'
import type { ElementOffset, Pixels } from '../../editor/compare.ts'
import { canvasSnapshot, designer } from '../../stores/designer.ts'
import { route } from '../../stores/router.ts'
import AppIcon from '../AppIcon.vue'
import ModalDialog from '../admin/ModalDialog.vue'

/**
 * 画布和服务端位置偏差的上限，mm（验收标准）。
 */
const LIMIT_MM = 0.2

/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 关闭对话框。
   */
  close: []
}>()

/**
 * 对话框状态。
 */
const state = reactive({
  /**
   * 预览的分辨率（默认用模板的）。
   */
  dpi: designer.template.page.dpi ?? 300,
  /**
   * 黑白输出。
   */
  mono: false,
  /**
   * 正在渲染。
   */
  busy: false,
  /**
   * 预览图的 blob URL。
   */
  url: '',
  /**
   * 预览图的宽度，像素。
   */
  width: 0,
  /**
   * 预览图的高度，像素。
   */
  height: 0,
  /**
   * 耗时说明（服务端各阶段之和、往返）。
   */
  ms: '',
  /**
   * 渲染警告（如字体降级）。
   */
  warnings: [] as string[],
  /**
   * 渲染或对比失败的原因。
   */
  error: '',
  /**
   * 叠加显示的画布快照（data URL）；没对比时为空。
   */
  overlay: '',
  /**
   * 每个元素的位置偏差，大的在前。
   */
  offsets: [] as ElementOffset[],
})
// 画布按模板 DPI 对齐条码模块，只有同样的 DPI 才能逐点对比。
/**
 * 模板的分辨率（画布按它对齐条码模块）。
 */
const templateDpi = computed(() => designer.template.page.dpi ?? 300)

/**
 * 能不能和画布对比：在设计页打开，且按模板的 DPI 预览。
 */
const canCompare = computed(() => route.view === 'designer' && state.dpi === templateDpi.value)

/**
 * 不能对比时的原因。
 */
const compareHint = computed(() => (route.view !== 'designer' ? '在设计页打开预览才能对比' : canCompare.value ? '' : `按模板的 ${templateDpi.value} dpi 预览才能对比`))

/**
 * 偏差最大的元素（只算两边都有内容的）。
 */
const worst = computed(() => state.offsets.find((item) => Number.isFinite(item.maxMm)))

/**
 * 偏差超过上限的元素。
 */
const failed = computed(() => state.offsets.filter((item) => item.maxMm > LIMIT_MM))

/**
 * 预览图的显示宽度：实际大小，最宽 760 像素。
 */
const cssWidth = computed(() => Math.min(760, (designer.template.page.width * 96) / 25.4))

onMounted(render)
onBeforeUnmount(() => URL.revokeObjectURL(state.url))

/**
 * 请服务端渲染当前模板，显示图片、耗时和警告。
 * @returns 渲染结束时完成（失败时显示原因，不抛）。
 */
async function render(): Promise<void> {
  state.busy = true
  state.error = ''
  state.overlay = ''
  state.offsets = []
  const started = performance.now()
  try {
    const response = await previewTemplate({ template: JSON.parse(JSON.stringify(designer.template)), dpi: state.dpi, mono: state.mono })
    const blob = await response.blob()
    URL.revokeObjectURL(state.url)
    state.url = URL.createObjectURL(blob)
    const serverMs = [...(response.headers.get('server-timing') ?? '').matchAll(/dur=([\d.]+)/g)].reduce((sum, match) => sum + Number(match[1]), 0)
    state.ms = `服务端 ${serverMs.toFixed(1)} ms · 往返 ${(performance.now() - started).toFixed(0)} ms`
    state.warnings = (response.headers.get('x-label-warnings') ?? '').split(',').filter(Boolean).map(decodeURIComponent)
    const image = await decode(state.url)
    state.width = image.naturalWidth
    state.height = image.naturalHeight
  } catch (error) {
    state.error = errorText(error)
  } finally {
    state.busy = false
  }
}

/**
 * 加载图片并等它解码完。
 * @param url 图片地址。
 * @returns 图片。
 */
function decode(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image()
    image.onload = () => resolve(image)
    image.onerror = () => reject(new Error('预览图片解码失败'))
    image.src = url
  })
}

/**
 * 把图片画到白底画布上取像素。
 * @param source 图片或画布。
 * @param width 宽度，像素。
 * @param height 高度，像素。
 * @returns 像素数据。
 */
function pixelsOf(source: CanvasImageSource, width: number, height: number): Pixels {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d', { willReadFrequently: true })!
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, width, height)
  context.drawImage(source, 0, 0, width, height)
  return context.getImageData(0, 0, width, height)
}

/**
 * 画布按服务端图片的像素尺寸导出，逐个元素量墨迹边界的差。画布要用样例值显示才可比。
 * @returns 对比结束时完成。
 */
async function compare(): Promise<void> {
  if (designer.ui.mode !== 'sample') designer.ui.mode = 'sample'
  // 等画布按新的显示方式同步完节点（导出时 toCanvas 会重新画，不依赖屏幕刷新）。
  await nextTick()
  await new Promise((resolve) => setTimeout(resolve, 50))
  const snapshot = canvasSnapshot(state.width, state.height)
  if (!snapshot) {
    state.error = '在设计页打开预览才能和画布对比'
    return
  }

  const server = pixelsOf(await decode(state.url), state.width, state.height)
  const canvas = pixelsOf(snapshot, state.width, state.height)
  state.offsets = compareElements(designer.template.elements, canvas, server, state.width / designer.template.page.width)
  state.overlay = snapshot.toDataURL('image/png')
}

/**
 * 偏差的显示文字。
 * @param item 一个元素的偏差。
 * @returns 如 0.12 mm；只有一边有内容时说明。
 */
function offsetText(item: ElementOffset): string {
  return Number.isFinite(item.maxMm) ? `${item.maxMm.toFixed(2)} mm` : '一边没有内容'
}
</script>

<template>
  <ModalDialog title="服务端预览" wide @close="emit('close')">
    <div style="display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-bottom: 12px">
      <div class="seg" role="group" aria-label="预览 DPI">
        <button v-for="d in [203, 300, 600]" :key="d" type="button" :class="{ on: state.dpi === d }" @click="(state.dpi = d), render()">{{ d }} dpi</button>
      </div>
      <label class="check"><input v-model="state.mono" type="checkbox" @change="render" />黑白（热敏打印效果）</label>
      <span class="hint">用样例值渲染当前内容（不用先保存），算法和接口一致</span>
      <div class="spacer"></div>
      <button class="btn sm" type="button" :disabled="!canCompare || !state.url || state.busy" :title="compareHint" @click="compare">
        <AppIcon name="layers" small />和画布对比
      </button>
    </div>
    <div v-if="state.error" class="notice danger"><AppIcon name="alert" /><div>{{ state.error }}</div></div>
    <div class="preview-stage">
      <div v-if="state.url" class="preview-pair" :style="{ width: cssWidth + 'px' }">
        <img :src="state.url" alt="服务端渲染结果" />
        <img v-if="state.overlay" class="preview-overlay" :src="state.overlay" alt="设计器画布（叠加）" />
      </div>
      <span v-else class="hint">{{ state.busy ? '正在渲染…' : '' }}</span>
    </div>
    <div v-for="w in state.warnings" :key="w" class="notice warn" style="margin-top: 10px"><AppIcon name="alert" /><div>{{ w }}</div></div>
    <template v-if="state.offsets.length">
      <div class="notice" :class="failed.length ? 'warn' : 'info'" style="margin-top: 10px">
        <AppIcon :name="failed.length ? 'alert' : 'check'" />
        <div>
          画布和服务端的元素位置：最大偏差 {{ worst ? offsetText(worst) : '—' }}（要求 ≤ {{ LIMIT_MM }} mm）。叠加图里画布半透明显示在上面。
          <template v-if="failed.length"><br />超出的元素：{{ failed.map((item) => `${item.name}（${offsetText(item)}）`).join('、') }}</template>
        </div>
      </div>
    </template>
    <dl class="kv-list">
      <dt>输出尺寸</dt>
      <dd>{{ state.width ? `${state.width} × ${state.height} 点（${state.dpi} dpi）` : '—' }}</dd>
      <dt>耗时</dt>
      <dd>{{ state.ms || '—' }}</dd>
      <dt>元素</dt>
      <dd>{{ designer.template.elements.filter((e) => !e.hidden).length }} 个（隐藏的不输出）</dd>
    </dl>
  </ModalDialog>
</template>

<script setup lang="ts">
/**
 * 拼版设计器右侧：版式属性（名称、纸张、格子、填充顺序、裁切线、打印偏移）、选中格子的位置和序号、检查结果、预览和测试页。
 */
import { computed, ref } from 'vue'
import { errorText } from '../../api/http.ts'
import { layoutTestPage, previewLayout } from '../../api/layouts.ts'
import type { PreviewItem } from '../../api/layouts.ts'
import { footprint, pageCount } from '../../model/layout.ts'
import { toast } from '../../stores/designer.ts'
import { commit, problems, renumberSlots, setOrder, sheet } from '../../stores/sheet.ts'
import AppIcon from '../AppIcon.vue'
import ModalDialog from '../admin/ModalDialog.vue'

defineProps<{
  /**
   * 只读：属性不能改。
   */
  readOnly: boolean
}>()

/**
 * 正在编辑的版式（这个面板只在打开版式后显示）。
 */
const layout = computed(() => sheet.layout!)
/**
 * 只选中一个格子时是它的下标，否则为 null。
 */
const selected = computed(() => (sheet.selection.length === 1 ? sheet.selection[0] : null))
/**
 * 选中的格子；没有只选中一个时为 null。
 */
const slot = computed(() => (selected.value === null ? null : layout.value.slots[selected.value]))
/**
 * 预览结果：PNG 直接显示，PDF 用浏览器自带的查看器显示在对话框里，都可以下载。
 */
const preview = ref<{ url: string; kind: 'png' | 'pdf'; title: string; fileName: string } | null>(null)
/**
 * 上传批量数据用的文件输入框。
 */
const fileInput = ref<HTMLInputElement | null>(null)
/**
 * 正在请求预览或测试页。
 */
const busy = ref(false)

/**
 * 设置选中格子的旋转角度。
 * @param value 0、90、180、270。
 */
function setRotation(value: number): void {
  if (!slot.value) return
  slot.value.rotation = value
  commit()
}

/**
 * 改选中格子的序号（从 1 开始），其他格子顺延。
 * @param event 输入框的 change 事件。
 */
function onOrder(event: Event): void {
  const value = Number((event.target as HTMLInputElement).value)
  if (selected.value !== null && Number.isInteger(value)) setOrder(selected.value, value - 1)
}

/**
 * 用样例值预览一页（PNG）。
 */
async function previewPage(): Promise<void> {
  await run(async () => {
    const response = await previewLayout({ layout: layout.value, templateCode: sheet.previewCode, format: 'png' })
    await show(response, 'png', `${layout.value.code}.png`)
  })
}

/**
 * 上传一批数据预览多页效果（PDF）。文件可以是批量请求体（有 items，可带 common）、data 数组，或 { data, copies } 数组。
 * @param event 文件输入框的 change 事件。
 */
async function previewFile(event: Event): Promise<void> {
  const file = (event.target as HTMLInputElement).files?.[0]
  ;(event.target as HTMLInputElement).value = ''
  if (!file) return
  await run(async () => {
    const items = parseItems(JSON.parse(await file.text()) as unknown)
    const response = await previewLayout({ layout: layout.value, templateCode: sheet.previewCode, format: 'pdf', items })
    await show(response, 'pdf', `${layout.value.code}-preview.pdf`)
  })
}

/**
 * 打印校准测试页（PDF）：每格边框、中心十字和序号。
 */
async function testPage(): Promise<void> {
  await run(async () => show(await layoutTestPage(layout.value), 'pdf', `${layout.value.code}-test-page.pdf`, '打印校准测试页（按实际大小 100% 打印）'))
}

/**
 * 把上传的 JSON 转成预览数据：批量请求体（items 加 common）、data 数组，或 { data, copies } 数组。
 * @param value 解析好的 JSON。
 * @returns 每条数据和份数。
 */
function parseItems(value: unknown): PreviewItem[] {
  const body = value as { items?: unknown; common?: Record<string, unknown> }
  const list = Array.isArray(value) ? value : Array.isArray(body?.items) ? body.items : null
  if (!list?.length) throw new Error('文件里没有数据：要么是批量请求体（有 items），要么是数据数组')
  return list.map((entry) => {
    const item = entry as { data?: Record<string, unknown>; copies?: number }
    const data = item && typeof item === 'object' && item.data && typeof item.data === 'object' ? item.data : (entry as Record<string, unknown>)
    return { data: { ...(body?.common ?? {}), ...data }, copies: typeof item?.copies === 'number' ? item.copies : undefined }
  })
}

/**
 * 把接口返回的文件显示在对话框里（新开窗口会被浏览器当成弹窗拦掉）。
 * @param response 接口的响应（PNG 或 PDF）。
 * @param kind 文件类型。
 * @param fileName 下载时的文件名。
 * @param title 对话框标题；不传时写张数和页数。
 * @returns 显示后完成。
 */
async function show(response: Response, kind: 'png' | 'pdf', fileName: string, title?: string): Promise<void> {
  const blob = await response.blob()
  closePreview()
  const count = `${response.headers.get('X-Label-Count') ?? '?'} 张 · ${response.headers.get('X-Page-Count') ?? '?'} 页`
  preview.value = { url: URL.createObjectURL(blob), kind, fileName, title: title ?? `预览：${count}` }
}

/**
 * 关闭预览对话框，释放文件的 blob URL。
 */
function closePreview(): void {
  if (preview.value) URL.revokeObjectURL(preview.value.url)
  preview.value = null
}

/**
 * 执行一次请求：期间显示忙碌，失败时提示。
 * @param action 请求。
 * @returns 请求结束时完成。
 */
async function run(action: () => Promise<void>): Promise<void> {
  busy.value = true
  try {
    await action()
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <aside class="panel rightpanel" aria-label="版式属性">
    <fieldset class="props fs" :disabled="readOnly">
      <template v-if="slot && selected !== null">
        <h4>第 {{ selected + 1 }} 个格子</h4>
        <div class="pgrid">
          <div class="cell"><span>X</span><div class="unit" data-unit="mm"><input v-model.number="slot.x" class="inp num" type="number" step="0.1" aria-label="格子 X" @change="commit()" /></div></div>
          <div class="cell"><span>Y</span><div class="unit" data-unit="mm"><input v-model.number="slot.y" class="inp num" type="number" step="0.1" aria-label="格子 Y" @change="commit()" /></div></div>
        </div>
        <div class="prow">
          <label for="sh-rot">旋转</label>
          <select id="sh-rot" :value="slot.rotation" class="sel" @change="setRotation(Number(($event.target as HTMLSelectElement).value))">
            <option v-for="r in [0, 90, 180, 270]" :key="r" :value="r">{{ r }}°</option>
          </select>
        </div>
        <div class="prow">
          <label for="sh-no">序号</label>
          <input id="sh-no" :value="selected + 1" class="inp num" type="number" min="1" :max="layout.slots.length" @change="onOrder" />
        </div>
        <p class="hint" style="margin-top: 0">占 {{ footprint(layout.slot, slot).width }} × {{ footprint(layout.slot, slot).height }} mm。改序号就是调整填充顺序。</p>
        <div class="pdiv"></div>
      </template>
      <h4>版式</h4>
      <div class="prow"><label for="sh-name">名称</label><input id="sh-name" v-model="layout.name" class="inp" @change="commit()" /></div>
      <div class="prow"><label for="sh-code">编码</label><input id="sh-code" :value="layout.code" class="inp mono" readonly title="编码保存后不能改" /></div>
      <div class="prow"><span class="muted">纸张</span><span class="mono">{{ layout.paper.width }} × {{ layout.paper.height }} mm</span></div>
      <div class="prow"><span class="muted">格子</span><span class="mono">{{ layout.slot.width }} × {{ layout.slot.height }} mm × {{ layout.slots.length }}</span></div>
      <div class="sect-h"><span>填充顺序</span></div>
      <div class="btn-row">
        <button class="btn" type="button" @click="renumberSlots('rows')">先行后列</button>
        <button class="btn" type="button" @click="renumberSlots('cols')">先列后行</button>
      </div>
      <div class="pdiv"></div>
      <h4>打印</h4>
      <label class="check"><input v-model="layout.cutLines" type="checkbox" @change="commit()" />画格子边框（白纸裁切用）</label>
      <div class="sect-h" style="margin-top: 10px"><span>打印偏移（打印出来偏右 1 mm 就填 X −1）</span></div>
      <div class="pgrid">
        <div class="cell"><span>X</span><div class="unit" data-unit="mm"><input v-model.number="layout.offset.x" class="inp num" type="number" step="0.1" aria-label="偏移 X" @change="commit()" /></div></div>
        <div class="cell"><span>Y</span><div class="unit" data-unit="mm"><input v-model.number="layout.offset.y" class="inp num" type="number" step="0.1" aria-label="偏移 Y" @change="commit()" /></div></div>
      </div>
      <label class="check"><input v-model="sheet.enabled" type="checkbox" @change="commit()" />启用（停用后开放接口不能用）</label>
    </fieldset>
    <div class="props">
      <div class="btn-row">
        <button class="btn" type="button" :disabled="busy" @click="testPage"><AppIcon name="download" small />打印测试页</button>
      </div>
      <p class="hint">先按偏移 0 打一张测试页，量出格子边框和纸上实际位置的差，再填打印偏移。</p>
      <div class="pdiv"></div>
      <h4>预览</h4>
      <div class="btn-row">
        <button class="btn" type="button" :disabled="busy || !sheet.previewCode || !!problems.messages.length" @click="previewPage"><AppIcon name="preview" small />样例值一页</button>
        <button class="btn" type="button" :disabled="busy || !sheet.previewCode || !!problems.messages.length" @click="fileInput?.click()"><AppIcon name="upload" small />上传数据</button>
      </div>
      <p class="hint">用左侧选中的模板预览。上传的 JSON 可以是批量请求体，也可以是数据数组；{{ layout.slots.length }} 格的版式 50 张是 {{ pageCount(layout.slots.length, 50) }} 页。</p>
      <input ref="fileInput" type="file" accept="application/json,.json" hidden @change="previewFile" />
      <template v-if="problems.messages.length">
        <div class="pdiv"></div>
        <h4 class="err-h"><AppIcon name="alert" small />不能保存</h4>
        <ul class="sheet-problems">
          <li v-for="message in problems.messages.slice(0, 8)" :key="message">{{ message }}</li>
          <li v-if="problems.messages.length > 8">还有 {{ problems.messages.length - 8 }} 条…</li>
        </ul>
      </template>
    </div>
    <ModalDialog v-if="preview" :title="preview.title" wide @close="closePreview">
      <img v-if="preview.kind === 'png'" :src="preview.url" alt="版式预览" class="sheet-preview" />
      <iframe v-else :src="preview.url" :title="preview.title" class="sheet-pdf"></iframe>
      <template #footer>
        <a class="btn" :href="preview.url" :download="preview.fileName"><AppIcon name="download" small />下载</a>
        <button class="btn primary" type="button" @click="closePreview">关闭</button>
      </template>
    </ModalDialog>
  </aside>
</template>

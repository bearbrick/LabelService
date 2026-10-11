<script setup lang="ts">
/**
 * 接口调试：用已发布的模板试一次单张生成（POST /api/admin/api-test/render，请求体和开放接口一样），
 * 显示状态、响应头、耗时和结果（PNG 图、PDF 内嵌预览、ZPL 文本）。不需要 API Key，不写调用日志。
 */
import { computed, onBeforeUnmount, reactive, watch } from 'vue'
import { getVersion, listVersions, testRender } from '../../api/admin.ts'
import type { VersionInfo } from '../../api/admin.ts'
import { ApiError, errorText } from '../../api/http.ts'
import { fileSize } from '../../model/format.ts'
import { sampleRequest } from '../../model/request.ts'
import type { LabelTemplate } from '../../model/template.ts'
import { copyText } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'

const props = defineProps<{ code: string }>()

interface Result {
  ok: boolean
  status: string
  ms: string
  size: string
  headers: [string, string][]
  url: string
  kind: 'png' | 'pdf' | 'zpl' | 'error'
  text: string
}

const state = reactive({
  versions: [] as VersionInfo[],
  template: null as LabelTemplate | null,
  body: '',
  error: '',
  busy: false,
  loading: false,
  result: null as Result | null,
})

const SHOWN_HEADERS = ['content-type', 'x-template-version', 'x-label-count', 'server-timing', 'x-label-warnings']

const parsed = computed<Record<string, unknown> | null>(() => {
  try {
    const value = JSON.parse(state.body) as unknown
    return value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : null
  } catch {
    return null
  }
})

const curl = computed(() => {
  const body = parsed.value ? JSON.stringify(parsed.value) : state.body
  return `curl -X POST "${location.origin}/api/v1/render" \\\n  -H "X-Api-Key: <你的 API Key>" \\\n  -H "Content-Type: application/json" \\\n  --data-binary '${body.replace(/'/g, "'\\''")}' \\\n  -o label.${String(parsed.value?.format ?? 'pdf')}`
})

watch(() => props.code, load, { immediate: true })
onBeforeUnmount(clearResult)

async function load(): Promise<void> {
  clearResult()
  state.loading = true
  state.error = ''
  state.template = null
  try {
    state.versions = (await listVersions(props.code)).filter((v) => v.status === 'published')
    const latest = state.versions[0]
    if (latest) state.template = await getVersion(props.code, latest.versionNo)
    reset()
  } catch (error) {
    state.error = errorText(error)
  } finally {
    state.loading = false
  }
}

function reset(): void {
  const template = state.template ?? { code: props.code, fields: [] }
  state.body = JSON.stringify(sampleRequest(template, String(parsed.value?.format ?? 'pdf')), null, 2)
}

function setField(key: string, value: unknown): void {
  const body = parsed.value
  if (!body) {
    state.error = '请求体不是合法的 JSON 对象'
    return
  }

  if (value === undefined) delete body[key]
  else body[key] = value
  state.body = JSON.stringify(body, null, 2)
}

/**
 * 去掉一个必填字段，看看接口怎么报错。
 */
function breakRequired(): void {
  const body = parsed.value
  const data = body?.data as Record<string, unknown> | undefined
  const field = state.template?.fields.find((f) => f.required !== false && data && f.key in data)
  if (!body || !data || !field) return
  delete data[field.key]
  state.body = JSON.stringify(body, null, 2)
}

function clearResult(): void {
  if (state.result?.url) URL.revokeObjectURL(state.result.url)
  state.result = null
}

async function send(): Promise<void> {
  if (!parsed.value) {
    state.error = '请求体不是合法的 JSON 对象'
    return
  }

  state.error = ''
  state.busy = true
  clearResult()
  const started = performance.now()
  try {
    const response = await testRender(parsed.value)
    const blob = await response.blob()
    const type = response.headers.get('content-type') ?? ''
    const serverMs = [...(response.headers.get('server-timing') ?? '').matchAll(/dur=([\d.]+)/g)].reduce((sum, match) => sum + Number(match[1]), 0)
    const kind = type.includes('png') ? 'png' : type.includes('pdf') ? 'pdf' : 'zpl'
    state.result = {
      ok: true,
      status: `${response.status} ${response.statusText || 'OK'}`,
      ms: `服务端 ${serverMs.toFixed(1)} ms · 往返 ${(performance.now() - started).toFixed(0)} ms`,
      size: fileSize(blob.size),
      headers: SHOWN_HEADERS.map((name) => [name, response.headers.get(name) ?? ''] as [string, string]).filter(([, value]) => value),
      url: kind === 'zpl' ? '' : URL.createObjectURL(blob),
      kind,
      text: kind === 'zpl' ? await blob.text() : '',
    }
  } catch (error) {
    const status = error instanceof ApiError ? `${error.status} ${error.code}` : '请求失败'
    const text = error instanceof ApiError ? [error.message, ...error.details.filter((item) => !item.includes(error.message))].join('\n') : errorText(error)
    state.result = { ok: false, status, ms: `往返 ${(performance.now() - started).toFixed(0)} ms`, size: '', headers: [], url: '', kind: 'error', text }
  } finally {
    state.busy = false
  }
}
</script>

<template>
  <div v-if="state.loading" class="hint">正在读取模板…</div>
  <div v-else-if="!state.versions.length" class="empty box">
    <AppIcon name="api" /><span>{{ state.error || `模板 ${props.code} 还没有发布。发布后才能调用接口。` }}</span>
  </div>
  <template v-else>
    <div class="two-col">
      <div class="box">
        <h3><AppIcon name="send" small />请求</h3>
        <div class="endpoint"><span class="method">POST</span>/api/v1/render<span class="muted" style="margin-left: auto">X-Api-Key: ••••••••</span></div>
        <div class="form-row">
          <span class="lb muted">格式</span>
          <div class="seg" role="group" aria-label="输出格式">
            <button v-for="f in ['pdf', 'png', 'zpl']" :key="f" type="button" :class="{ on: parsed?.format === f }" @click="setField('format', f)">{{ f.toUpperCase() }}</button>
          </div>
        </div>
        <div class="form-row">
          <label for="api-ver">版本</label>
          <select id="api-ver" class="sel" style="max-width: 220px" :value="parsed?.version ?? ''" @change="setField('version', ($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : undefined)">
            <option value="">最新发布（v{{ state.versions[0].versionNo }}）</option>
            <option v-for="v in state.versions" :key="v.versionNo" :value="v.versionNo">v{{ v.versionNo }}</option>
          </select>
        </div>
        <div class="form-row">
          <label for="api-dpi">DPI</label>
          <select id="api-dpi" class="sel" style="max-width: 220px" :value="parsed?.dpi ?? ''" @change="setField('dpi', ($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : undefined)">
            <option value="">模板默认（{{ state.template?.page.dpi ?? 300 }}）</option>
            <option :value="203">203</option>
            <option :value="300">300</option>
            <option :value="600">600</option>
          </select>
        </div>
        <label for="api-body" class="hint">请求体 JSON，可以直接改</label>
        <textarea id="api-body" v-model="state.body" class="json" spellcheck="false"></textarea>
        <div v-if="state.error" class="form-err">{{ state.error }}</div>
        <div style="display: flex; gap: 8px; margin-top: 10px; flex-wrap: wrap">
          <button class="btn" type="button" @click="reset"><AppIcon name="refresh" small />重置为样例</button>
          <button class="btn" type="button" @click="breakRequired">去掉一个必填字段</button>
          <div class="spacer"></div>
          <button class="btn primary" type="button" :disabled="state.busy" @click="send"><AppIcon name="send" small />{{ state.busy ? '生成中…' : '发送请求' }}</button>
        </div>
      </div>
      <div class="box">
        <h3><AppIcon name="preview" small />响应</h3>
        <div v-if="!state.result" class="empty"><AppIcon name="send" /><span>点“发送请求”查看返回结果</span></div>
        <template v-else>
          <div class="resp-status">
            <span class="pill" :class="state.result.ok ? 'ok' : 'danger'">{{ state.result.status }}</span>
            <span class="muted num">{{ state.result.ms }}</span>
            <span v-if="state.result.size" class="muted">· {{ state.result.size }}</span>
          </div>
          <div v-if="state.result.headers.length" class="resp-head"><div v-for="[k, v] in state.result.headers" :key="k">{{ k }}: {{ v }}</div></div>
          <div v-if="state.result.kind === 'png'" class="pages"><div class="pageimg"><img :src="state.result.url" alt="生成的 PNG" /></div></div>
          <iframe v-else-if="state.result.kind === 'pdf'" class="pdf-frame" :src="state.result.url" title="生成的 PDF"></iframe>
          <div v-else-if="state.result.kind === 'zpl'" style="margin-top: 14px">
            <div class="code-head">
              <span class="hint">ZPL 文本</span><button class="btn sm" type="button" @click="copyText(state.result.text)"><AppIcon name="copy" small />复制全部</button>
            </div>
            <pre class="code" style="white-space: pre-wrap; word-break: break-all">{{ state.result.text.length > 4000 ? state.result.text.slice(0, 4000) + '\n…' : state.result.text }}</pre>
          </div>
          <pre v-else class="code" style="white-space: pre-wrap">{{ state.result.text }}</pre>
          <a v-if="state.result.url" class="btn sm" :href="state.result.url" :download="`${props.code}.${state.result.kind}`" style="margin-top: 10px"><AppIcon name="download" small />下载文件</a>
        </template>
      </div>
    </div>
    <div class="box" style="margin-top: 16px">
      <div class="code-head">
        <h3 style="margin: 0">curl</h3>
        <button class="btn sm" type="button" @click="copyText(curl)"><AppIcon name="copy" small />复制</button>
      </div>
      <pre class="code">{{ curl }}</pre>
    </div>
  </template>
</template>

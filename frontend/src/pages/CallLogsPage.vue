<script setup lang="ts">
/**
 * 调用日志：按时间、结果、调用方、模板筛选，按时间倒序分批加载（翻页用上一批最后一条）；点一行看详情和各阶段耗时。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { listClients, searchCallLogs } from '../api/admin.ts'
import type { ApiClient, CallLog, CallLogFilter } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { fmtDateTime } from '../model/format.ts'
import { catalog } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { copyText } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { can } from '../stores/session.ts'

/**
 * 每批加载的条数。
 */
const PAGE_SIZE = 50

/**
 * 时间范围的选项。
 */
const RANGES = [
  { value: 'today', label: '今天' },
  { value: '7d', label: '近 7 天' },
  { value: '30d', label: '近 30 天' },
  { value: 'all', label: '全部' },
]

/**
 * 筛选条件（改了 300 毫秒后重新加载）。
 */
const filter = reactive({
  /**
   * 时间范围：today、7d、30d、all。
   */
  range: '7d',
  /**
   * 结果：ok、error，空表示全部。
   */
  status: '',
  /**
   * 调用方 ID 的文本，空表示全部。
   */
  clientId: '',
  /**
   * 模板编码，空表示全部。
   */
  templateCode: '',
  /**
   * 搜索：链路 ID、X-Request-Id、模板编码、错误码。
   */
  q: '',
})

/**
 * 已加载的日志，新的在前。
 */
const logs = ref<CallLog[]>([])

/**
 * 调用方（筛选用，没有 api.manage 权限时为空）。
 */
const clients = ref<ApiClient[]>([])

/**
 * 加载状态。
 */
const state = reactive({
  /**
   * 正在加载。
   */
  loading: false,
  /**
   * 后面可能还有（上一批取满了）。
   */
  more: false,
})

/**
 * 正在看详情的日志。
 */
const detail = ref<CallLog | null>(null)

/**
 * 已加载部分的汇总：调用次数、成功率、成功生成的张数、成功调用的平均耗时。
 */
const summary = computed(() => {
  const list = logs.value
  const ok = list.filter((l) => l.status === 'ok')
  return {
    calls: list.length,
    rate: list.length ? ((ok.length / list.length) * 100).toFixed(1) : '—',
    labels: ok.reduce((sum, l) => sum + l.itemCount, 0),
    ms: ok.length ? (ok.reduce((sum, l) => sum + l.durationMs, 0) / ok.length).toFixed(1) : '—',
  }
})

/**
 * 详情里的各阶段耗时（阶段名和毫秒）。
 */
const stages = computed(() => {
  if (!detail.value) return []
  try {
    return Object.entries(JSON.parse(detail.value.stageMsJson) as Record<string, number>)
  } catch {
    return []
  }
})

onMounted(async () => {
  if (can('api.manage')) clients.value = await listClients().catch(() => [])
  await load()
})

/**
 * 筛选条件变化后延迟加载的计时器。
 */
let timer: ReturnType<typeof setTimeout> | undefined
watch(() => ({ ...filter }), () => {
  clearTimeout(timer)
  timer = setTimeout(() => void load(), 300)
})

/**
 * 时间范围的开始时间。
 * @returns ISO 时间；全部时为 undefined。
 */
function from(): string | undefined {
  const now = new Date()
  if (filter.range === 'today') return new Date(now.getFullYear(), now.getMonth(), now.getDate()).toISOString()
  if (filter.range === '7d') return new Date(now.getTime() - 7 * 86400e3).toISOString()
  if (filter.range === '30d') return new Date(now.getTime() - 30 * 86400e3).toISOString()
  return undefined
}

/**
 * 按筛选条件加载一批日志。
 * @param append true 时接着上一批往后加载，否则从头加载。
 * @returns 加载结束时完成。
 */
async function load(append = false): Promise<void> {
  const last = append ? logs.value.at(-1) : undefined
  const query: CallLogFilter = {
    from: from(),
    status: filter.status || undefined,
    clientId: filter.clientId ? Number(filter.clientId) : undefined,
    templateCode: filter.templateCode || undefined,
    q: filter.q.trim() || undefined,
    beforeTime: last?.createdAt,
    beforeId: last?.id,
    pageSize: PAGE_SIZE,
  }
  state.loading = true
  try {
    const page = await searchCallLogs(query)
    logs.value = append ? [...logs.value, ...page] : page
    state.more = page.length === PAGE_SIZE
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    state.loading = false
  }
}

/**
 * 恢复默认的筛选条件（近 7 天、全部）。
 */
function reset(): void {
  Object.assign(filter, { range: '7d', status: '', clientId: '', templateCode: '', q: '' })
}

/**
 * 结果的显示文字。
 * @param log 日志。
 * @returns 成功、客户端断开或错误码。
 */
function resultText(log: CallLog): string {
  return log.status === 'ok' ? '成功' : log.status === 'aborted' ? '客户端断开' : log.errorCode ?? '失败'
}

/**
 * 把已加载的日志复制成 CSV 文本（可以贴进表格软件）。
 */
function csv(): void {
  const head = ['时间', '调用方', '接口', '模板', '版本', '格式', '张数', '耗时ms', '结果', '错误码', 'traceId']
  const rows = logs.value.map((l) => [fmtDateTime(l.createdAt), l.clientName, l.endpoint, l.templateCode, l.versionNo ?? '', l.format, l.itemCount, l.durationMs, l.status, l.errorCode ?? '', l.traceId])
  void copyText([head, ...rows].map((row) => row.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(',')).join('\n'))
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>日志</span><span class="sep">/</span><span>调用日志</span></div>
          <h1>调用日志 <small>CALL LOGS</small></h1>
          <p>每次调用生成接口都会记一条：调用方、模板版本、张数、耗时和结果。不保存业务数据。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn" type="button" :disabled="!logs.length" @click="csv"><AppIcon name="copy" small />复制为 CSV</button>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="时间范围">
          <button v-for="r in RANGES" :key="r.value" type="button" :class="{ on: filter.range === r.value }" @click="filter.range = r.value">{{ r.label }}</button>
        </div>
        <select id="lf-status" v-model="filter.status" class="sel" aria-label="结果"><option value="">全部结果</option><option value="ok">成功</option><option value="error">失败</option></select>
        <select v-if="clients.length" id="lf-client" v-model="filter.clientId" class="sel" aria-label="调用方">
          <option value="">全部调用方</option>
          <option v-for="c in clients" :key="c.id" :value="String(c.id)">{{ c.name }}</option>
        </select>
        <select id="lf-tpl" v-model="filter.templateCode" class="sel" aria-label="模板">
          <option value="">全部模板</option>
          <option v-for="t in catalog.templates" :key="t.code" :value="t.code">{{ t.code }}</option>
        </select>
        <input id="lf-q" v-model.trim="filter.q" class="inp grow" placeholder="搜索 traceId、X-Request-Id、模板编码、错误码" aria-label="搜索" />
        <button class="btn ghost" type="button" @click="reset">重置</button>
      </div>
      <div class="kpis" style="grid-template-columns: repeat(auto-fit, minmax(160px, 1fr))">
        <div class="kpi"><span class="k">调用次数</span><span class="v">{{ summary.calls.toLocaleString() }}<small>次</small></span><span class="d">已加载的记录</span></div>
        <div class="kpi"><span class="k">成功率</span><span class="v">{{ summary.rate }}<small>%</small></span></div>
        <div class="kpi"><span class="k">生成张数</span><span class="v">{{ summary.labels.toLocaleString() }}<small>张</small></span></div>
        <div class="kpi"><span class="k">平均耗时</span><span class="v">{{ summary.ms }}<small>ms</small></span><span class="d">成功的调用</span></div>
      </div>
      <div class="table-box">
        <div class="tw" tabindex="0" role="region" aria-label="调用日志表格">
          <table class="dt call-table">
            <colgroup><col style="width: 180px" /><col style="width: 200px" /><col style="width: 70px" /><col style="width: 300px" /><col style="width: 70px" /><col style="width: 60px" /><col style="width: 100px" /><col style="width: 170px" /><col style="width: 150px" /></colgroup>
            <thead>
              <tr><th>时间</th><th>调用方</th><th>接口</th><th>模板</th><th>格式</th><th>张数</th><th>耗时</th><th>结果</th><th>traceId</th></tr>
            </thead>
            <tbody>
              <tr v-for="l in logs" :key="l.id" class="click" @click="detail = l">
                <td class="nowrap num">{{ fmtDateTime(l.createdAt) }}</td>
                <td class="nowrap">{{ l.clientName || '—' }}</td>
                <td class="nowrap">{{ l.endpoint.includes('batch') ? '批量' : '单张' }}</td>
                <td class="nowrap"><span class="code-chip">{{ l.templateCode }}</span><span v-if="l.versionNo" class="muted"> v{{ l.versionNo }}</span></td>
                <td>{{ l.format.toUpperCase() }}</td>
                <td class="num">{{ l.itemCount }}</td>
                <td class="num nowrap" :class="{ warn: l.isSlow }">{{ l.durationMs.toFixed(1) }} ms</td>
                <td class="nowrap"><span class="pill" :class="l.status === 'ok' ? 'ok' : 'danger'">{{ resultText(l) }}</span></td>
                <td class="code-chip nowrap">{{ l.traceId.slice(0, 12) }}…</td>
              </tr>
              <tr v-if="!logs.length && !state.loading"><td colspan="9" class="muted" style="text-align: center; padding: 32px">没有符合条件的调用</td></tr>
            </tbody>
          </table>
        </div>
        <div class="table-foot">
          <span>已加载 {{ logs.length }} 条，按时间倒序</span>
          <button v-if="state.more" class="btn sm" type="button" :disabled="state.loading" @click="load(true)">{{ state.loading ? '加载中…' : '加载更多' }}</button>
        </div>
      </div>
    </div>
    <template v-if="detail">
      <div class="drawer-back" @click="detail = null"></div>
      <aside class="drawer" role="dialog" aria-modal="true" aria-label="调用详情">
        <div class="modal-h"><h3>调用详情</h3><button class="icon-btn" type="button" aria-label="关闭" @click="detail = null"><AppIcon name="x" /></button></div>
        <div class="modal-b">
          <div class="resp-status">
            <span class="pill" :class="detail.status === 'ok' ? 'ok' : 'danger'">{{ resultText(detail) }}</span><span class="muted num">{{ fmtDateTime(detail.createdAt) }}</span>
          </div>
          <dl class="kv2">
            <dt>调用方</dt><dd>{{ detail.clientName || '—' }}</dd>
            <dt>接口</dt><dd class="code-chip">POST {{ detail.endpoint }}</dd>
            <dt>模板</dt><dd><span class="code-chip">{{ detail.templateCode }}</span><span v-if="detail.versionNo"> · v{{ detail.versionNo }}</span></dd>
            <dt>格式</dt><dd>{{ detail.format.toUpperCase() }}</dd>
            <dt>张数</dt><dd class="num">{{ detail.itemCount }}</dd>
            <dt>耗时</dt><dd class="num">{{ detail.durationMs.toFixed(1) }} ms{{ detail.isSlow ? '（慢调用）' : '' }}</dd>
            <dt>来源 IP</dt><dd class="code-chip">{{ detail.ip || '—' }}</dd>
            <dt>traceId</dt><dd class="code-chip">{{ detail.traceId }}</dd>
            <dt>X-Request-Id</dt><dd class="code-chip">{{ detail.requestId || '—' }}</dd>
            <template v-if="detail.status !== 'ok'"><dt>错误码</dt><dd class="code-chip">{{ detail.errorCode || '—' }}</dd><dt>说明</dt><dd>{{ detail.errorMessage || '—' }}</dd></template>
          </dl>
          <template v-if="stages.length">
            <div class="code-head"><span class="hint">各阶段耗时（ms）</span></div>
            <pre class="code">{{ stages.map(([k, v]) => `${k.padEnd(9)} ${Number(v).toFixed(2)}`).join('\n') }}</pre>
          </template>
        </div>
        <div class="modal-f">
          <button class="btn" type="button" @click="copyText(detail.traceId)"><AppIcon name="copy" small />复制 traceId</button>
        </div>
      </aside>
    </template>
  </div>
</template>

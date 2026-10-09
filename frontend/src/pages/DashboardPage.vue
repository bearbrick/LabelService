<script setup lang="ts">
/** 工作台：今天的调用、近 14 天调用量、近 7 天调用最多的模板、待处理事项、最近的操作。没有 log.view 时只显示模板统计。 */
import { computed, onMounted, ref } from 'vue'
import { getDashboard } from '../api/admin.ts'
import type { Dashboard } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { barChart, CHART } from '../model/chart.ts'
import { fmtTime } from '../model/format.ts'
import { catalog } from '../stores/catalog.ts'
import { dialogs } from '../stores/dialogs.ts'
import { go, goBiz } from '../stores/router.ts'
import { can, session } from '../stores/session.ts'

const data = ref<Dashboard | null>(null)
const error = ref('')
const hover = ref(-1)

onMounted(async () => {
  if (!can('tpl.view')) return
  try {
    data.value = await getDashboard()
  } catch (e) {
    error.value = errorText(e)
  }
})

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 6 ? '夜深了' : h < 11 ? '早上好' : h < 13 ? '中午好' : h < 18 ? '下午好' : '晚上好'
})
const todayText = computed(() => {
  const d = new Date()
  return `${d.getFullYear()} 年 ${d.getMonth() + 1} 月 ${d.getDate()} 日 星期${'日一二三四五六'[d.getDay()]}`
})

const kpis = computed(() => {
  const t = data.value?.templates
  const today = data.value?.today
  const list: { label: string; value: string | number; unit: string; note: string }[] = [
    { label: '模板', value: t?.total ?? 0, unit: '个', note: `${t?.published ?? 0} 个已发布` },
    { label: '待发布改动', value: t?.changed ?? 0, unit: '个', note: `${t?.neverPublished ?? 0} 个从未发布` },
  ]
  if (today) {
    const rate = today.total ? (((today.total - today.errors) / today.total) * 100).toFixed(1) : '—'
    list.push(
      { label: '今日调用', value: today.total, unit: '次', note: `生成 ${today.labels.toLocaleString()} 张` },
      { label: '成功率', value: rate, unit: today.total ? '%' : '', note: `失败 ${today.errors} 次` },
      { label: '平均耗时', value: Number(today.averageMs.toFixed(1)), unit: 'ms', note: `慢调用 ${today.slow} 次（超过 100 ms）` },
    )
  }

  return list
})

const chart = computed(() => barChart((data.value?.days ?? []).map((d) => ({ date: d.date, ok: d.total - d.errors, fail: d.errors }))))
const top = computed(() => {
  const list = data.value?.topTemplates ?? []
  const max = Math.max(1, ...list.map((r) => r.labels))
  return list.map((r) => ({ ...r, name: catalog.templates.find((t) => t.code === r.code)?.name ?? r.code, pct: (r.labels / max) * 100 }))
})
const todos = computed(() => {
  const items: { tag: string; cls: string; text: string; action: string; run: () => void }[] = []
  const changed = catalog.templates.filter((t) => t.latestVersion > 0 && t.hasDraft)
  const fresh = catalog.templates.filter((t) => t.latestVersion === 0)
  if (changed.length) items.push({ tag: '待发布', cls: 'accent', text: `${changed.length} 个模板有未发布的改动，接口仍在用旧版本`, action: '查看', run: () => goBiz('all') })
  if (fresh.length) items.push({ tag: '未发布', cls: 'warn', text: `${fresh.length} 个模板还没有发布，接口不能调用`, action: '查看', run: () => goBiz('all') })
  if (data.value?.today?.errors) items.push({ tag: '失败', cls: 'danger', text: `今天有 ${data.value.today.errors} 次调用失败`, action: '调用日志', run: () => go('calllogs') })
  return items
})
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><span>工作台</span></div>
          <h1>{{ greeting }}，{{ session.me?.name }} <small>DASHBOARD</small></h1>
          <p>{{ todayText }}{{ data?.today ? ' · 调用数字来自调用日志' : '' }}</p>
        </div>
        <div class="spacer"></div>
        <button v-if="can('api.test')" class="btn" type="button" @click="go('apitest')"><AppIcon name="api" small />接口调试</button>
        <button class="btn primary" type="button" :disabled="!can('tpl.edit')" @click="dialogs.newTemplate = true"><AppIcon name="plus" small />新建模板</button>
      </div>
      <div v-if="error" class="notice warn"><AppIcon name="alert" /><div>{{ error }}</div></div>
      <div v-if="!can('tpl.view')" class="empty box"><AppIcon name="lock" /><span>当前账号没有查看模板的权限，工作台没有可显示的内容。</span></div>
      <template v-else-if="data">
        <div class="kpis">
          <div v-for="k in kpis" :key="k.label" class="kpi">
            <span class="k">{{ k.label }}</span>
            <span class="v">{{ k.value }}<small>{{ k.unit }}</small></span>
            <span class="d">{{ k.note }}</span>
          </div>
        </div>
        <div v-if="data.days" class="grid-2">
          <div class="box">
            <div class="code-head">
              <h3 style="margin: 0">近 14 天调用次数</h3>
              <div class="legend"><span><i style="background: var(--chart-ok)"></i>成功</span><span><i style="background: var(--chart-fail)"></i>失败</span></div>
            </div>
            <div class="chart-wrap" @mouseleave="hover = -1">
              <svg :viewBox="`0 0 ${CHART.W} ${CHART.H}`" role="img" aria-label="近 14 天每天成功和失败的调用次数柱状图">
                <g v-for="t in chart.ticks" :key="'t' + t.v">
                  <line class="grid-l" :x1="CHART.L" :x2="CHART.W - CHART.R" :y1="t.y" :y2="t.y"></line>
                  <text class="ax" :x="CHART.L - 8" :y="t.y + 4" text-anchor="end">{{ t.v }}</text>
                </g>
                <g v-for="(b, i) in chart.bars" :key="b.key">
                  <rect v-if="hover === i" class="hl" :x="b.x - 5" :y="CHART.T" :width="b.w + 10" :height="CHART.H - CHART.T - CHART.B" rx="4"></rect>
                  <path class="ok" :d="b.okPath"></path>
                  <path v-if="b.failPath" class="fail" :d="b.failPath"></path>
                  <text v-if="i % 2 === 1" class="ax" :x="b.x + b.w / 2" :y="CHART.H - 8" text-anchor="middle">{{ b.label }}</text>
                  <rect class="hit" :x="b.x - b.pad" :y="CHART.T" :width="b.w + b.pad * 2" :height="CHART.H - CHART.T - CHART.B" @mouseenter="hover = i"></rect>
                </g>
                <line class="base-l" :x1="CHART.L" :x2="CHART.W - CHART.R" :y1="CHART.H - CHART.B" :y2="CHART.H - CHART.B"></line>
              </svg>
              <div v-if="hover >= 0 && chart.bars[hover]" class="chart-tip" :style="{ left: chart.bars[hover].tipLeft, top: chart.bars[hover].tipTop, marginTop: '-8px' }">
                <b>{{ chart.bars[hover].full }}</b>
                <div><span>成功</span><span>{{ chart.bars[hover].ok }} 次</span></div>
                <div><span>失败</span><span>{{ chart.bars[hover].fail }} 次</span></div>
              </div>
            </div>
            <details style="margin-top: 6px">
              <summary class="hint" style="cursor: pointer">按天查看数据</summary>
              <div class="tw" style="margin-top: 8px">
                <table class="grid">
                  <thead><tr><th>日期</th><th>成功</th><th>失败</th></tr></thead>
                  <tbody><tr v-for="b in chart.bars" :key="'r' + b.key"><td>{{ b.full }}</td><td class="num">{{ b.ok }}</td><td class="num">{{ b.fail }}</td></tr></tbody>
                </table>
              </div>
            </details>
          </div>
          <div class="stack">
            <div class="box">
              <h3>近 7 天生成最多的模板</h3>
              <div v-if="top.length" class="bar-list">
                <div v-for="r in top" :key="r.code" class="bar-row">
                  <span>{{ r.name }} <span class="code-chip">{{ r.code }}</span></span>
                  <span class="num muted">{{ r.labels.toLocaleString() }} 张</span>
                  <div class="track"><div class="fill" :style="{ width: r.pct + '%' }"></div></div>
                </div>
              </div>
              <div v-else class="hint">近 7 天没有调用</div>
            </div>
            <div class="box">
              <h3>需要处理</h3>
              <div v-for="t in todos" :key="t.text" class="todo">
                <span class="pill" :class="t.cls">{{ t.tag }}</span><span style="flex: 1">{{ t.text }}</span>
                <button class="link" type="button" @click="t.run()">{{ t.action }}</button>
              </div>
              <div v-if="!todos.length" class="hint">没有待处理的事项</div>
            </div>
          </div>
        </div>
        <div v-if="data.recentOps?.length" class="box" style="margin-top: 16px">
          <div class="code-head">
            <h3 style="margin: 0">最近的操作</h3>
            <button class="btn sm" type="button" @click="go('oplogs')">查看全部</button>
          </div>
          <div class="tw">
            <table class="dt">
              <thead><tr><th>时间</th><th>操作人</th><th>类型</th><th>对象</th><th>说明</th></tr></thead>
              <tbody>
                <tr v-for="op in data.recentOps" :key="op.id">
                  <td class="nowrap num muted">{{ fmtTime(op.createdAt) }}</td>
                  <td class="nowrap">{{ op.userName }}</td>
                  <td class="nowrap"><span class="pill">{{ op.type }}</span></td>
                  <td>{{ op.target }}</td>
                  <td>{{ op.detail }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

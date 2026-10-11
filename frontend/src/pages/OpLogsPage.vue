<script setup lang="ts">
/**
 * 操作日志：谁在什么时候做了什么（登录、模板发布、密钥和权限变更等），分批加载。
 */
import { onMounted, reactive, ref, watch } from 'vue'
import { listUsers, searchOpLogs } from '../api/admin.ts'
import type { OpLog, User } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import { fmtDateTime } from '../model/format.ts'
import { toast } from '../stores/designer.ts'
import { go } from '../stores/router.ts'
import { can } from '../stores/session.ts'

/**
 * 每批加载的条数。
 */
const PAGE_SIZE = 50

/**
 * 操作类型的选项（和服务端记录的类型一致）。
 */
const TYPES = ['登录', '退出', '个人设置', '新建模板', '编辑模板', '发布模板', '回滚版本', '删除模板', '业务分组', '画布预设', '素材', '调用方', '用户', '角色']

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
   * 操作人的用户 ID 的文本，空表示全部。
   */
  userId: '',
  /**
   * 操作类型，空表示全部。
   */
  type: '',
  /**
   * 搜索：对象和说明。
   */
  q: '',
})

/**
 * 已加载的日志，新的在前。
 */
const logs = ref<OpLog[]>([])

/**
 * 用户（筛选用，没有 sys.user 权限时为空）。
 */
const users = ref<User[]>([])

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

onMounted(async () => {
  if (can('sys.user')) users.value = await listUsers().catch(() => [])
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
  state.loading = true
  try {
    const page = await searchOpLogs({
      from: from(),
      userId: filter.userId ? Number(filter.userId) : undefined,
      type: filter.type || undefined,
      q: filter.q.trim() || undefined,
      beforeId: append ? logs.value.at(-1)?.id : undefined,
      pageSize: PAGE_SIZE,
    })
    logs.value = append ? [...logs.value, ...page] : page
    state.more = page.length === PAGE_SIZE
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    state.loading = false
  }
}

/**
 * 操作类型标签的样式：发布绿色，删除和权限、密钥相关的黄色。
 * @param type 操作类型。
 * @returns 样式类名；普通操作为空字符串。
 */
function typeClass(type: string): string {
  if (type === '发布模板') return 'ok'
  if (type === '删除模板' || type === '调用方' || type === '角色' || type === '用户') return 'warn'
  return ''
}

/**
 * 恢复默认的筛选条件（近 7 天、全部）。
 */
function reset(): void {
  Object.assign(filter, { range: '7d', userId: '', type: '', q: '' })
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>日志</span><span class="sep">/</span><span>操作日志</span></div>
          <h1>操作日志 <small>AUDIT</small></h1>
          <p>谁在什么时候做了什么：登录、模板发布、字段修改、密钥和权限变更。</p>
        </div>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="时间范围">
          <button v-for="r in RANGES" :key="r.value" type="button" :class="{ on: filter.range === r.value }" @click="filter.range = r.value">{{ r.label }}</button>
        </div>
        <select v-if="users.length" id="of-user" v-model="filter.userId" class="sel" aria-label="用户">
          <option value="">全部用户</option>
          <option v-for="u in users" :key="u.id" :value="String(u.id)">{{ u.name }}</option>
        </select>
        <select id="of-type" v-model="filter.type" class="sel" aria-label="操作类型">
          <option value="">全部操作</option>
          <option v-for="t in TYPES" :key="t" :value="t">{{ t }}</option>
        </select>
        <input id="of-q" v-model.trim="filter.q" class="inp grow" placeholder="搜索用户、对象或详情" aria-label="搜索" />
        <button class="btn ghost" type="button" @click="reset">重置</button>
      </div>
      <div class="table-box">
        <div class="tw" tabindex="0" role="region" aria-label="操作日志表格">
          <table class="dt audit-table">
            <colgroup><col style="width: 180px" /><col style="width: 130px" /><col style="width: 120px" /><col style="width: 280px" /><col style="width: 280px" /><col style="width: 140px" /></colgroup>
            <thead><tr><th>时间</th><th>用户</th><th>操作</th><th>对象</th><th>详情</th><th>IP</th></tr></thead>
            <tbody>
              <tr v-for="o in logs" :key="o.id">
                <td class="nowrap num">{{ fmtDateTime(o.createdAt) }}</td>
                <td class="nowrap">{{ o.userName || '—' }}</td>
                <td class="nowrap"><span class="pill" :class="typeClass(o.type)">{{ o.type }}</span></td>
                <td class="nowrap">{{ o.target }}</td>
                <td>{{ o.detail }}</td>
                <td class="code-chip nowrap">{{ o.ip }}</td>
              </tr>
              <tr v-if="!logs.length && !state.loading"><td colspan="6" class="muted" style="text-align: center; padding: 32px">没有符合条件的记录</td></tr>
            </tbody>
          </table>
        </div>
        <div class="table-foot">
          <span>已加载 {{ logs.length }} 条，按时间倒序</span>
          <button v-if="state.more" class="btn sm" type="button" :disabled="state.loading" @click="load(true)">{{ state.loading ? '加载中…' : '加载更多' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

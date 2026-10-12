<script setup lang="ts">
/**
 * 模板库：按业务分组的模板列表（卡片或表格），可筛选状态、排序；复制、删除模板。模板多时分页，每页 48 个。
 */
import { computed, reactive, ref, watch } from 'vue'
import { createTemplate, deleteTemplate, getTemplate } from '../api/admin.ts'
import type { TemplateItem } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import TemplateThumb from '../components/admin/TemplateThumb.vue'
import { fmtTime, sizeText } from '../model/format.ts'
import { LIBRARY_PAGE_SIZE, pageButtons, paginate } from '../model/templateList.ts'
import { businessOf, catalog, reloadTemplates, templateState } from '../stores/catalog.ts'
import type { TemplateState } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox, dialogs } from '../stores/dialogs.ts'
import { library } from '../stores/library.ts'
import { go, goBiz, openWorkspace, route } from '../stores/router.ts'
import { can, inScope } from '../stores/session.ts'

/**
 * 状态筛选的选项。
 */
const STATUS: { value: 'all' | TemplateState; label: string }[] = [
  { value: 'all', label: '全部' },
  { value: 'published', label: '已发布' },
  { value: 'changed', label: '有未发布改动' },
  { value: 'draft', label: '未发布' },
]

/**
 * 列表的筛选和显示设置。
 */
const filter = reactive({
  /**
   * 按状态筛选。
   */
  status: 'all' as 'all' | TemplateState,
  /**
   * 排序：updated 最近修改、name 名称、code 编码。
   */
  sort: 'updated',
  /**
   * 显示方式：卡片或表格。
   */
  mode: 'cards' as 'cards' | 'table',
})

/**
 * 要看的页码（从 1 开始）。
 */
const page = ref(1)

/**
 * 复制模板的对话框。
 */
const copy = reactive({
  /**
   * 复制对话框是否打开。
   */
  open: false,
  /**
   * 被复制的模板。
   */
  source: null as TemplateItem | null,
  /**
   * 新模板的编码。
   */
  code: '',
  /**
   * 新模板的名称。
   */
  name: '',
  /**
   * 复制失败的原因。
   */
  error: '',
  /**
   * 正在复制。
   */
  busy: false,
})

/**
 * 正在看的业务分组；看全部时为 null。
 */
const currentBiz = computed(() => (route.biz === 'all' ? null : businessOf(route.biz)))

/**
 * 当前分组里的全部模板（还没按状态和搜索过滤）。
 */
const base = computed(() => catalog.templates.filter((t) => route.biz === 'all' || (t.businessId ?? 0) === route.biz))

/**
 * 过滤并排序后的模板。
 */
const list = computed(() => {
  const q = library.query.trim().toLowerCase()
  const items = base.value.filter((t) => (filter.status === 'all' || templateState(t).key === filter.status) && (!q || t.name.toLowerCase().includes(q) || t.code.toLowerCase().includes(q)))
  const sorters: Record<string, (a: TemplateItem, b: TemplateItem) => number> = {
    updated: (a, b) => b.updatedAt.localeCompare(a.updatedAt),
    name: (a, b) => a.name.localeCompare(b.name, 'zh'),
    code: (a, b) => a.code.localeCompare(b.code),
  }
  return [...items].sort(sorters[filter.sort])
})

/**
 * 当前页的模板和页数。
 */
const paged = computed(() => paginate(list.value, page.value, LIBRARY_PAGE_SIZE))

// 换分组、筛选、排序后回到第一页
watch([() => route.biz, () => filter.status, () => filter.sort, () => library.query], () => (page.value = 1))

/**
 * 翻到某一页并回到列表顶部。
 * @param target 页码。
 */
function goPage(target: number): void {
  page.value = target
  // 列表自己滚动时滚的是列表，窗口矮时滚的是整页：都回到顶部
  document.querySelectorAll('.page-scroll, .page-body.fill, .table-box.fill > .tw').forEach((el) => el.scrollTo({ top: 0 }))
}

/**
 * 某个状态的模板个数（筛选按钮上显示）。
 * @param status 状态；all 是全部。
 * @returns 个数。
 */
function count(status: 'all' | TemplateState): number {
  return status === 'all' ? base.value.length : base.value.filter((t) => templateState(t).key === status).length
}

/**
 * 当前用户能不能编辑这个模板（有 tpl.edit 且在自己的业务范围里）。
 * @param t 模板。
 * @returns 能编辑时为 true。
 */
function canEditTemplate(t: TemplateItem): boolean {
  return can('tpl.edit') && inScope(t.businessId)
}

/**
 * 打开复制对话框，默认编码加 _COPY、名称加（副本）。
 * @param t 被复制的模板。
 */
function startCopy(t: TemplateItem): void {
  Object.assign(copy, { open: true, source: t, code: `${t.code}_COPY`.slice(0, 64), name: `${t.name}（副本）`, error: '', busy: false })
}

/**
 * 复制模板：取原模板当前的内容（有草稿时是草稿），以新编码、新名称新建到同一个分组。
 * @returns 复制结束时完成（失败时显示原因，不抛）。
 */
async function doCopy(): Promise<void> {
  const source = copy.source
  if (!source) return
  copy.busy = true
  copy.error = ''
  try {
    const { content } = await getTemplate(source.code)
    const { code } = await createTemplate({ code: copy.code, name: copy.name.trim(), businessId: source.businessId ?? catalog.businesses[0]?.id ?? 0, content: { ...content, code: copy.code, name: copy.name.trim() } })
    await reloadTemplates()
    copy.open = false
    toast(`已复制为 ${code}`)
  } catch (error) {
    copy.error = errorText(error)
  } finally {
    copy.busy = false
  }
}

/**
 * 删除模板（确认后）。
 * @param t 模板。
 * @returns 操作结束时完成。
 */
async function remove(t: TemplateItem): Promise<void> {
  const text = `删除后开放接口立即不能再调用 ${t.code}，编码也不能再用。确定删除“${t.name}”？`
  if (!(await confirmBox('删除模板', text, '删除', true))) return
  try {
    await deleteTemplate(t.code)
    await reloadTemplates()
    toast(`已删除 ${t.code}`)
  } catch (error) {
    toast(errorText(error), true)
  }
}

</script>

<template>
  <div class="page-scroll fixed-head">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb">
            <button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><button type="button" @click="goBiz('all')">模板</button>
            <template v-if="currentBiz"><span class="sep">/</span><span>{{ currentBiz.name }}</span></template>
          </div>
          <h1>{{ currentBiz ? currentBiz.name : '全部模板' }} <small>{{ currentBiz ? '业务编码 ' + currentBiz.code : 'TEMPLATES' }}</small></h1>
          <p>{{ currentBiz ? currentBiz.description || '按业务分组管理的标签模板。' : '按业务分组管理标签和唛头模板。业务系统用模板编码加数据调用接口，拿回标签文件。' }}</p>
        </div>
        <div class="spacer"></div>
        <button v-if="currentBiz && can('biz.manage')" class="btn" type="button" @click="go('business')"><AppIcon name="edit" small />管理业务</button>
        <button class="btn primary" type="button" :disabled="!can('tpl.edit')" :title="can('tpl.edit') ? '' : '当前账号没有新建模板的权限'" @click="dialogs.newTemplate = true">
          <AppIcon name="plus" small />新建模板
        </button>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="按状态筛选">
          <button v-for="s in STATUS" :key="s.value" type="button" :class="{ on: filter.status === s.value }" @click="filter.status = s.value">
            {{ s.label }} <span class="num" style="opacity: 0.6">{{ count(s.value) }}</span>
          </button>
        </div>
        <input id="tpl-search" v-model="library.query" class="inp grow" placeholder="按名称或编码筛选" aria-label="筛选模板" />
        <select id="tpl-sort" v-model="filter.sort" class="sel" aria-label="排序"><option value="updated">最近更新</option><option value="name">按名称</option><option value="code">按编码</option></select>
        <div class="spacer"></div>
        <div class="seg" role="group" aria-label="显示方式">
          <button type="button" :class="{ on: filter.mode === 'cards' }" @click="filter.mode = 'cards'"><AppIcon name="cards" small />卡片</button>
          <button type="button" :class="{ on: filter.mode === 'table' }" @click="filter.mode = 'table'"><AppIcon name="table" small />列表</button>
        </div>
      </div>

      <div v-if="!list.length" class="empty box">
        <AppIcon name="folder" />
        <span>{{ library.query || filter.status !== 'all' ? '没有符合条件的模板' : '这个业务下还没有模板' }}</span>
        <button v-if="can('tpl.edit') && !library.query" class="btn primary" type="button" @click="dialogs.newTemplate = true">新建模板</button>
      </div>

      <div v-else-if="filter.mode === 'cards'" class="page-body fill">
        <div class="cards">
          <div v-for="t in paged.items" :key="t.code" class="card">
            <div class="thumb" @click="openWorkspace(t.code)"><TemplateThumb :code="t.code" :updated-at="t.updatedAt" :alt="`${t.name} 缩略图`" /></div>
            <div class="card-body">
              <div class="card-title"><b :title="t.name">{{ t.name }}</b><span class="pill" :class="templateState(t).cls">{{ templateState(t).short }}</span></div>
              <div class="card-meta"><span class="mono" :title="t.code">{{ t.code }}</span><span>{{ sizeText(t) }} · {{ t.dpi }} dpi</span></div>
              <div class="card-meta">
                <span class="mini-chip" :title="businessOf(t.businessId).name"><span class="biz-dot" :style="{ background: businessOf(t.businessId).color }"></span><span class="tx">{{ businessOf(t.businessId).name }}</span></span>
                <span>{{ t.updatedBy || '—' }} · {{ fmtTime(t.updatedAt) }}</span>
              </div>
              <div class="card-actions">
                <button class="btn sm primary" type="button" @click="openWorkspace(t.code)">{{ canEditTemplate(t) ? '设计' : '查看' }}</button>
                <button class="btn sm" type="button" @click="openWorkspace(t.code, 'fields')">数据源</button>
                <button v-if="can('api.test')" class="btn sm" type="button" @click="openWorkspace(t.code, 'api')">接口调试</button>
                <div class="spacer"></div>
                <button v-if="can('tpl.edit')" class="icon-btn xs" type="button" data-tip="复制模板" aria-label="复制模板" @click="startCopy(t)"><AppIcon name="copy" small /></button>
                <button v-if="can('tpl.delete')" class="icon-btn xs" type="button" data-tip="删除模板" aria-label="删除模板" @click="remove(t)"><AppIcon name="trash" small /></button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="table-box fill">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>模板</th><th>编码</th><th>业务</th><th>画布</th><th>状态</th><th>字段 / 元素</th><th>更新</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr v-for="t in paged.items" :key="t.code" class="click" @click="openWorkspace(t.code)">
                <td class="nowrap"><b style="font-weight: 600; color: #172236">{{ t.name }}</b></td>
                <td class="code-chip">{{ t.code }}</td>
                <td><span class="mini-chip" :title="businessOf(t.businessId).name"><span class="biz-dot" :style="{ background: businessOf(t.businessId).color }"></span><span class="tx">{{ businessOf(t.businessId).name }}</span></span></td>
                <td class="nowrap num">{{ sizeText(t) }} · {{ t.dpi }} dpi</td>
                <td><span class="pill" :class="templateState(t).cls">{{ templateState(t).short }}</span></td>
                <td class="num">{{ t.fieldCount }} / {{ t.elementCount }}</td>
                <td class="nowrap muted">{{ t.updatedBy || '—' }} · {{ fmtTime(t.updatedAt) }}</td>
                <td class="actions" @click.stop>
                  <button class="link" type="button" @click="openWorkspace(t.code, 'fields')">数据源</button>
                  <button v-if="can('tpl.edit')" class="link" type="button" @click="startCopy(t)">复制</button>
                  <button v-if="can('tpl.delete')" class="link danger" type="button" @click="remove(t)">删除</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="paged.pages > 1" class="list-pager">
        <span class="hint">共 {{ list.length }} 个 · 第 {{ paged.page }} / {{ paged.pages }} 页</span>
        <nav class="pager" aria-label="翻页">
          <button type="button" :disabled="paged.page <= 1" aria-label="上一页" @click="goPage(paged.page - 1)"><AppIcon name="chevL" small /></button>
          <template v-for="(p, i) in pageButtons(paged.page, paged.pages)" :key="i">
            <span v-if="p === 0" class="gap">…</span>
            <button v-else type="button" :class="{ on: p === paged.page }" :aria-current="p === paged.page ? 'page' : undefined" @click="goPage(p)">{{ p }}</button>
          </template>
          <button type="button" :disabled="paged.page >= paged.pages" aria-label="下一页" @click="goPage(paged.page + 1)"><AppIcon name="chevR" small /></button>
        </nav>
      </div>
    </div>
    <ModalDialog v-if="copy.open" title="复制模板" width="min(460px, 100%)" @close="copy.open = false">
      <p class="hint" style="margin-top: 0">复制“{{ copy.source?.name }}”当前的内容（有草稿时复制草稿），新模板是未发布的草稿。</p>
      <div class="form-row"><label for="cp-name">名称</label><input id="cp-name" v-model="copy.name" class="inp" /></div>
      <div class="form-row"><label for="cp-code">编码</label><input id="cp-code" v-model.trim="copy.code" class="inp mono" /></div>
      <div v-if="copy.error" class="form-err">{{ copy.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="copy.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="copy.busy" @click="doCopy">复制</button>
      </template>
    </ModalDialog>
  </div>
</template>

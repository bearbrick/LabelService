<script setup lang="ts">
/**
 * 左侧模板树：只做导航，模板多了也不会长得翻不动。顶部是最近打开；业务分组每组最多列 20 个最近修改的模板，再多进模板库看；
 * 搜索时平铺列出匹配的模板。窄屏时还放功能菜单。右边缘可以拖动改宽度（名称很长时拖宽看全）。
 */
import { computed, reactive, ref, watch } from 'vue'
import type { TemplateItem } from '../../api/admin.ts'
import { sizeText } from '../../model/format.ts'
import { EXPAND_ALL_LIMIT, GROUP_LIMIT, groupTemplates, SEARCH_LIMIT, searchTemplates, visibleInGroup } from '../../model/templateList.ts'
import { businessOf, catalog } from '../../stores/catalog.ts'
import { dialogs } from '../../stores/dialogs.ts'
import { library } from '../../stores/library.ts'
import { loadRecentTemplates, recentTemplates } from '../../stores/recentTemplates.ts'
import { go, goBiz, openWorkspace, route } from '../../stores/router.ts'
import type { Page } from '../../stores/router.ts'
import { can, session } from '../../stores/session.ts'
import { useSidebarWidth } from '../../composables/useSidebarWidth.ts'
import AppIcon from '../AppIcon.vue'
import TreeTemplateItem from './TreeTemplateItem.vue'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 收起成窄条（模板工作区里）。
   */
  collapsed: boolean
}>()

/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 点了一项（窄屏时父组件收起模板树）。
   */
  navigate: []
}>()

/**
 * 模板树宽度和拖动手柄。
 */
const sidebar = useSidebarWidth()

/**
 * 搜索框的内容（按名称或编码过滤）。
 */
const query = ref('')

/**
 * 每个业务分组是否展开（分组 ID → 展开；0 是未分组）。
 */
const openFolders = reactive<Record<number, boolean>>({})

/**
 * 当前打开的模板编码；不在模板工作区时为空。
 */
const currentCode = computed(() => (route.page === 'workspace' ? route.code : ''))

/**
 * 认识的业务分组 ID；模板的分组不在里面时归到“未分组”。
 */
const knownIds = computed(() => new Set(catalog.businesses.map((b) => b.id)))

/**
 * 模板归到哪个分组。
 * @param template 模板。
 * @returns 分组 ID；0 是未分组。
 */
function groupOf(template: TemplateItem): number {
  return template.businessId !== null && knownIds.value.has(template.businessId) ? template.businessId : 0
}

/**
 * 按分组归类、最近修改在前的模板。
 */
const grouped = computed(() => groupTemplates(catalog.templates, groupOf))

/**
 * 树的第一层：业务分组，有不属于任何分组的模板时加一个“未分组”。每个分组带上要显示的模板和总数。
 */
const folders = computed(() => {
  const list = catalog.businesses.map((b) => ({ id: b.id, name: b.name, code: b.code }))
  if (grouped.value.has(0)) list.push({ id: 0, name: '未分组', code: '-' })
  return list.map((folder) => ({ ...folder, ...visibleInGroup(grouped.value.get(folder.id) ?? [], GROUP_LIMIT, currentCode.value) }))
})

// 加载完以后定每个分组默认展不展开：模板少时全部展开（和以前一样），多了收起
watch(
  () => [catalog.loaded, folders.value.map((folder) => folder.id)] as const,
  ([loaded, ids]) => {
    if (loaded) ids.forEach((id) => (openFolders[id] ??= catalog.templates.length <= EXPAND_ALL_LIMIT))
  },
  { immediate: true },
)

// 打开模板时展开它所在的分组，在树里能看到它
watch(
  () => [currentCode.value, catalog.loaded] as const,
  ([code]) => {
    const template = code ? catalog.templates.find((t) => t.code === code) : undefined
    if (template) openFolders[groupOf(template)] = true
  },
  { immediate: true },
)

// 换账号时读取那个账号的最近打开
watch(() => session.me?.account ?? '', loadRecentTemplates, { immediate: true })

/**
 * 最近打开的模板（已删除、看不到的不显示）。
 */
const recent = computed(() => {
  const byCode = new Map(catalog.templates.map((t) => [t.code, t]))
  return recentTemplates.codes.map((code) => byCode.get(code)).filter((t): t is TemplateItem => !!t)
})

/**
 * 搜索结果：平铺，最多 30 个。
 */
const results = computed(() => searchTemplates(catalog.templates, query.value, SEARCH_LIMIT))

/**
 * 展开或收起分组，并在模板库里打开它（清掉模板库的筛选关键字，看整个分组）。
 * @param id 分组 ID；0 是未分组（只展开收起）。
 */
function openFolder(id: number): void {
  openFolders[id] = !openFolders[id]
  if (id) showGroup(id)
  else emit('navigate')
}

/**
 * 在模板库里看一个分组（或全部）的模板，清掉模板库的筛选关键字。
 * @param id 分组 ID；0（未分组）或 all 时看全部。
 */
function showGroup(id: number | 'all'): void {
  library.query = ''
  goBiz(id || 'all')
  emit('navigate')
}

/**
 * 在模板库里看全部搜索结果（把关键字带过去）。
 */
function showAllResults(): void {
  library.query = query.value
  goBiz('all')
  emit('navigate')
}

/**
 * 打开模板工作区：在工作区里切换模板时停留在当前页签（版本记录页签除外）。
 * @param template 模板。
 */
function open(template: TemplateItem): void {
  openWorkspace(template.code, route.page === 'workspace' && route.view !== 'versions' ? route.view : 'designer')
  emit('navigate')
}

/**
 * 打开功能页面（窄屏菜单）。
 * @param target 页面。
 */
function page(target: Page): void {
  go(target)
  emit('navigate')
}
</script>

<template>
  <aside class="sidebar" :class="{ collapsed: props.collapsed, resizing: sidebar.dragging.value }" :style="{ '--sidebar-w': `${sidebar.width.value}px` }" aria-label="模板树">
    <div
      class="sidebar-resizer"
      role="separator"
      aria-orientation="vertical"
      aria-label="拖动调整模板树宽度，双击恢复默认"
      title="拖动调整宽度，双击恢复默认"
      tabindex="0"
      :aria-valuenow="sidebar.width.value"
      :aria-valuemin="sidebar.minWidth"
      :aria-valuemax="sidebar.maxWidth"
      @pointerdown="sidebar.startDrag"
      @dblclick="sidebar.reset"
      @keydown="sidebar.onKey"
    ></div>
    <div class="sidebar-heading">
      <h2>模板</h2>
      <button class="btn ghost sm" type="button" :disabled="!can('tpl.edit')" @click="dialogs.newTemplate = true"><AppIcon name="plus" small />新建模板</button>
    </div>
    <div class="search-box"><AppIcon name="search" small /><input id="tree-q" v-model.trim="query" placeholder="搜索模板名称或编码" aria-label="搜索模板" /></div>
    <div class="table-tree">
      <template v-if="query">
        <div class="tree-caption">搜索结果<span>{{ results.total }}</span></div>
        <TreeTemplateItem v-for="t in results.items" :key="t.code" :template="t" :sub="`${t.code} · ${businessOf(t.businessId).name}`" :active="currentCode === t.code" flat @open="open(t)" />
        <button v-if="results.total > results.items.length" class="tree-more flat" type="button" @click="showAllResults">在模板库里查看全部 {{ results.total }} 个结果<AppIcon name="chevR" small /></button>
        <div v-if="!results.total" class="hint" style="padding: 16px 8px">没有找到“{{ query }}”</div>
      </template>
      <template v-else>
        <template v-if="recent.length">
          <div class="tree-caption">最近打开</div>
          <TreeTemplateItem v-for="t in recent" :key="'recent-' + t.code" :template="t" :sub="`${t.code} · ${sizeText(t)}`" :active="currentCode === t.code" flat @open="open(t)" />
          <div class="tree-caption">业务分组</div>
        </template>
        <button class="module-row" type="button" :class="{ active: route.page === 'templates' && route.biz === 'all' }" @click="showGroup('all')">
          <AppIcon name="folder" /><span class="nm">全部模板</span><span class="count">{{ catalog.templates.length }}</span>
        </button>
        <template v-for="folder in folders" :key="folder.id">
          <button class="module-row" type="button" :class="{ active: route.page === 'templates' && route.biz === folder.id }" :title="`${folder.name}（${folder.code}）`" @click="openFolder(folder.id)">
            <AppIcon :name="openFolders[folder.id] ? 'chevD' : 'chevR'" small class="chev" />
            <AppIcon :name="openFolders[folder.id] ? 'folderOpen' : 'folder'" />
            <span class="nm">{{ folder.name }}（{{ folder.code }}）</span><span class="count">{{ folder.total }}</span>
          </button>
          <template v-if="openFolders[folder.id]">
            <TreeTemplateItem v-for="t in folder.items" :key="t.code" :template="t" :sub="`${t.code} · ${sizeText(t)}`" :active="currentCode === t.code" @open="open(t)" />
            <button v-if="folder.total > GROUP_LIMIT" class="tree-more" type="button" :title="`这里只列最近修改的 ${GROUP_LIMIT} 个`" @click="showGroup(folder.id)">查看全部 {{ folder.total }} 个<AppIcon name="chevR" small /></button>
          </template>
        </template>
      </template>
    </div>
    <div class="mobile-nav">
      <div class="nav-title">功能</div>
      <button class="module-row" type="button" @click="page('dashboard')"><AppIcon name="dashboard" /><span class="nm">工作台</span></button>
      <button class="module-row" type="button" @click="page('presets')"><AppIcon name="ruler" /><span class="nm">画布预设</span></button>
      <button class="module-row" type="button" @click="page('assets')"><AppIcon name="image" /><span class="nm">素材与字体</span></button>
      <button v-if="can('api.manage')" class="module-row" type="button" @click="page('clients')"><AppIcon name="key" /><span class="nm">调用方与密钥</span></button>
      <button v-if="can('api.test')" class="module-row" type="button" @click="page('apitest')"><AppIcon name="send" /><span class="nm">接口调试</span></button>
      <button v-if="can('log.view')" class="module-row" type="button" @click="page('calllogs')"><AppIcon name="activity" /><span class="nm">调用日志</span></button>
      <button v-if="can('log.view')" class="module-row" type="button" @click="page('oplogs')"><AppIcon name="logs" /><span class="nm">操作日志</span></button>
      <button v-if="can('sys.user')" class="module-row" type="button" @click="page('users')"><AppIcon name="users" /><span class="nm">用户管理</span></button>
      <button v-if="can('sys.role')" class="module-row" type="button" @click="page('roles')"><AppIcon name="shield" /><span class="nm">角色权限</span></button>
      <button v-if="can('sys.settings')" class="module-row" type="button" @click="page('settings')"><AppIcon name="gear" /><span class="nm">系统信息</span></button>
    </div>
    <div class="sidebar-footer">
      <span><AppIcon name="box" small />{{ catalog.templates.length }} 个模板 · {{ catalog.businesses.length }} 个业务</span>
    </div>
    <button v-if="can('biz.manage')" class="project-add" type="button" @click="page('business')"><AppIcon name="plus" small />管理业务分组</button>
  </aside>
</template>

<script setup lang="ts">
/**
 * 左侧模板树：按业务分组列出模板，可搜索；窄屏时还放功能菜单。右边缘可以拖动改宽度（名称很长时拖宽看全）。
 */
import { computed, reactive, ref, watch } from 'vue'
import type { TemplateItem } from '../../api/admin.ts'
import { sizeText } from '../../model/format.ts'
import { catalog, templateState } from '../../stores/catalog.ts'
import { dialogs } from '../../stores/dialogs.ts'
import { go, goBiz, openWorkspace, route } from '../../stores/router.ts'
import type { Page } from '../../stores/router.ts'
import { can } from '../../stores/session.ts'
import { useSidebarWidth } from '../../composables/useSidebarWidth.ts'
import AppIcon from '../AppIcon.vue'

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
 * 每个业务分组是否展开（分组 ID → 展开），新出现的分组默认展开。
 */
const openFolders = reactive<Record<number, boolean>>({})

watch(
  () => catalog.businesses.map((b) => b.id),
  (ids) => ids.forEach((id) => (openFolders[id] ??= true)),
  { immediate: true },
)

/**
 * 树的第一层：业务分组，有不属于任何分组的模板时加一个“未分组”。
 */
const folders = computed(() => {
  const ids = new Set(catalog.businesses.map((b) => b.id))
  const list = catalog.businesses.map((b) => ({ id: b.id, name: b.name, code: b.code }))
  if (catalog.templates.some((t) => t.businessId === null || !ids.has(t.businessId))) list.push({ id: 0, name: '未分组', code: '-' })
  return list
})

/**
 * 某个分组下符合搜索条件的模板，按名称排序。
 * @param id 分组 ID；0 是未分组。
 * @returns 模板。
 */
function templatesOf(id: number): TemplateItem[] {
  const q = query.value.toLowerCase()
  return catalog.templates
    .filter((t) => (t.businessId ?? 0) === id || (id === 0 && !catalog.businesses.some((b) => b.id === t.businessId)))
    .filter((t) => !q || t.name.toLowerCase().includes(q) || t.code.toLowerCase().includes(q))
    .sort((a, b) => a.name.localeCompare(b.name, 'zh'))
}

/**
 * 搜索没有任何结果。
 */
const noMatch = computed(() => !!query.value && folders.value.every((folder) => !templatesOf(folder.id).length))

/**
 * 展开或收起分组，并在模板库里打开它。
 * @param id 分组 ID；0 是未分组（只展开收起）。
 */
function openFolder(id: number): void {
  openFolders[id] = !openFolders[id]
  if (id) goBiz(id)
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
      <button class="module-row" type="button" :class="{ active: route.page === 'templates' && route.biz === 'all' }" @click="goBiz('all'), emit('navigate')">
        <AppIcon name="folder" /><span class="nm">全部模板</span><span class="count">{{ catalog.templates.length }}</span>
      </button>
      <template v-for="folder in folders" :key="folder.id">
        <button class="module-row" type="button" :class="{ active: route.page === 'templates' && route.biz === folder.id }" :title="`${folder.name}（${folder.code}）`" @click="openFolder(folder.id)">
          <AppIcon :name="openFolders[folder.id] || query ? 'chevD' : 'chevR'" small class="chev" />
          <AppIcon :name="openFolders[folder.id] || query ? 'folderOpen' : 'folder'" />
          <span class="nm">{{ folder.name }}（{{ folder.code }}）</span><span class="count">{{ templatesOf(folder.id).length }}</span>
        </button>
        <template v-if="openFolders[folder.id] || query">
          <button
            v-for="t in templatesOf(folder.id)"
            :key="t.code"
            class="table-item"
            type="button"
            :class="{ active: route.page === 'workspace' && route.code === t.code }"
            :title="`${t.name}
${t.code} · ${sizeText(t)}`"
            @click="open(t)"
          >
            <span><span class="state-dot" :class="templateState(t).key"></span>{{ t.name }}</span>
            <small>{{ t.code }} · {{ sizeText(t) }}</small>
          </button>
        </template>
      </template>
      <div v-if="noMatch" class="hint" style="padding: 16px 8px">没有找到“{{ query }}”</div>
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

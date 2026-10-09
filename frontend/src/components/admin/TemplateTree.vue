<script setup lang="ts">
/** 左侧模板树：按业务分组列出模板，可搜索；窄屏时还放功能菜单。 */
import { computed, reactive, ref, watch } from 'vue'
import type { TemplateItem } from '../../api/admin.ts'
import { sizeText } from '../../model/format.ts'
import { catalog, templateState } from '../../stores/catalog.ts'
import { dialogs } from '../../stores/dialogs.ts'
import { go, goBiz, openWorkspace, route } from '../../stores/router.ts'
import type { Page } from '../../stores/router.ts'
import { can } from '../../stores/session.ts'
import AppIcon from '../AppIcon.vue'

const props = defineProps<{ collapsed: boolean }>()
const emit = defineEmits<{ navigate: [] }>()

const query = ref('')
const openFolders = reactive<Record<number, boolean>>({})

watch(
  () => catalog.businesses.map((b) => b.id),
  (ids) => ids.forEach((id) => (openFolders[id] ??= true)),
  { immediate: true },
)

const folders = computed(() => {
  const ids = new Set(catalog.businesses.map((b) => b.id))
  const list = catalog.businesses.map((b) => ({ id: b.id, name: b.name, code: b.code }))
  if (catalog.templates.some((t) => t.businessId === null || !ids.has(t.businessId))) list.push({ id: 0, name: '未分组', code: '-' })
  return list
})

function templatesOf(id: number): TemplateItem[] {
  const q = query.value.toLowerCase()
  return catalog.templates
    .filter((t) => (t.businessId ?? 0) === id || (id === 0 && !catalog.businesses.some((b) => b.id === t.businessId)))
    .filter((t) => !q || t.name.toLowerCase().includes(q) || t.code.toLowerCase().includes(q))
    .sort((a, b) => a.name.localeCompare(b.name, 'zh'))
}

const noMatch = computed(() => !!query.value && folders.value.every((folder) => !templatesOf(folder.id).length))

function openFolder(id: number): void {
  openFolders[id] = !openFolders[id]
  if (id) goBiz(id)
  emit('navigate')
}

function open(template: TemplateItem): void {
  openWorkspace(template.code, route.page === 'workspace' && route.view !== 'versions' ? route.view : 'designer')
  emit('navigate')
}

function page(target: Page): void {
  go(target)
  emit('navigate')
}
</script>

<template>
  <aside class="sidebar" :class="{ collapsed: props.collapsed }" aria-label="模板树">
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
        <button class="module-row" type="button" :class="{ active: route.page === 'templates' && route.biz === folder.id }" @click="openFolder(folder.id)">
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
    </div>
    <div class="sidebar-footer">
      <span><AppIcon name="box" small />{{ catalog.templates.length }} 个模板 · {{ catalog.businesses.length }} 个业务</span>
    </div>
    <button v-if="can('biz.manage')" class="project-add" type="button" @click="page('business')"><AppIcon name="plus" small />管理业务分组</button>
  </aside>
</template>

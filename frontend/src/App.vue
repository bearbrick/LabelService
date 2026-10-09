<script setup lang="ts">
/**
 * 应用外壳：没登录时显示登录页；登录后是顶栏、左侧模板树和当前页面（hash 路由，见 stores/router.ts）。
 * 必须先改密码的账号只能进个人设置。页面要的权限点不满足时显示“没有权限”，服务端也会拒绝。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { Component } from 'vue'
import { errorText } from './api/http.ts'
import AppIcon from './components/AppIcon.vue'
import DialogHost from './components/admin/DialogHost.vue'
import NewTemplateDialog from './components/admin/NewTemplateDialog.vue'
import TemplateTree from './components/admin/TemplateTree.vue'
import TopBar from './components/admin/TopBar.vue'
import ApiTestPage from './pages/ApiTestPage.vue'
import AssetsPage from './pages/AssetsPage.vue'
import BusinessPage from './pages/BusinessPage.vue'
import CallLogsPage from './pages/CallLogsPage.vue'
import ClientsPage from './pages/ClientsPage.vue'
import DashboardPage from './pages/DashboardPage.vue'
import LayoutsPage from './pages/LayoutsPage.vue'
import LoginPage from './pages/LoginPage.vue'
import OpLogsPage from './pages/OpLogsPage.vue'
import PresetsPage from './pages/PresetsPage.vue'
import ProfilePage from './pages/ProfilePage.vue'
import RolesPage from './pages/RolesPage.vue'
import TemplatesPage from './pages/TemplatesPage.vue'
import UsersPage from './pages/UsersPage.vue'
import WorkspacePage from './pages/WorkspacePage.vue'
import { loadAssets } from './stores/assets.ts'
import { clearCatalog, loadCatalog } from './stores/catalog.ts'
import { designer, saveDraft, toast } from './stores/designer.ts'
import { dialogs } from './stores/dialogs.ts'
import { route } from './stores/router.ts'
import type { Page } from './stores/router.ts'
import { can, restoreSession, session } from './stores/session.ts'

const PAGES: Record<Page, { component: Component; permission?: string }> = {
  dashboard: { component: DashboardPage },
  templates: { component: TemplatesPage, permission: 'tpl.view' },
  workspace: { component: WorkspacePage, permission: 'tpl.view' },
  business: { component: BusinessPage },
  presets: { component: PresetsPage },
  layouts: { component: LayoutsPage },
  assets: { component: AssetsPage },
  clients: { component: ClientsPage, permission: 'api.manage' },
  apitest: { component: ApiTestPage, permission: 'api.test' },
  calllogs: { component: CallLogsPage, permission: 'log.view' },
  oplogs: { component: OpLogsPage, permission: 'log.view' },
  users: { component: UsersPage, permission: 'sys.user' },
  roles: { component: RolesPage, permission: 'sys.role' },
  profile: { component: ProfilePage },
}

const navOpen = ref(false)
const mustChange = computed(() => !!session.me?.mustChangePassword)
const current = computed(() => (mustChange.value ? PAGES.profile : PAGES[route.page]))
const allowed = computed(() => !current.value.permission || can(current.value.permission))

watch(
  // 换了用户，或改完首次登录的密码（之前没有任何权限）时重新加载共用列表。
  () => (session.me ? `${session.me.id}|${session.me.mustChangePassword}` : ''),
  async (key) => {
    if (!key) {
      clearCatalog()
      return
    }

    await Promise.all([loadCatalog(), loadAssets()]).catch((error) => toast(errorText(error), true))
  },
)

function onBeforeUnload(event: BeforeUnloadEvent): void {
  if (!designer.dirty) return
  void saveDraft()
  event.preventDefault()
}

onMounted(() => {
  void restoreSession()
  window.addEventListener('beforeunload', onBeforeUnload)
})

onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))
</script>

<template>
  <template v-if="session.ready">
    <LoginPage v-if="!session.me" />
    <div v-else class="app" :class="{ 'nav-open': navOpen }">
      <TopBar @toggle-nav="navOpen = !navOpen" />
      <div class="app-body">
        <TemplateTree v-if="!mustChange" :collapsed="dialogs.navCollapsed && route.page === 'workspace'" @navigate="navOpen = false" />
        <div class="nav-scrim" @click="navOpen = false"></div>
        <main class="main">
          <component :is="current.component" v-if="allowed" />
          <div v-else class="page-scroll">
            <div class="page-inner">
              <div class="empty box"><AppIcon name="lock" /><span>当前账号没有访问这个页面的权限，需要的话请联系系统管理员。</span></div>
            </div>
          </div>
        </main>
      </div>
    </div>
  </template>
  <NewTemplateDialog v-if="dialogs.newTemplate && session.me" />
  <DialogHost />
  <div class="toasts" aria-live="polite">
    <div v-for="item in designer.toasts" :key="item.id" class="toast" :class="{ err: item.error }">
      <AppIcon :name="item.error ? 'alert' : 'check'" small />{{ item.text }}
    </div>
  </div>
</template>

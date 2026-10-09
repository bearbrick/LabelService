<script setup lang="ts">
/** 顶栏：业务切换、功能菜单（资源、接口、日志、权限管理）、用户菜单。菜单项按权限显示。 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { businessOf, catalog, templateCountByBusiness } from '../../stores/catalog.ts'
import { designer } from '../../stores/designer.ts'
import { go, goBiz, route } from '../../stores/router.ts'
import type { Page } from '../../stores/router.ts'
import { can, session, signOut } from '../../stores/session.ts'
import AppIcon from '../AppIcon.vue'

defineEmits<{ toggleNav: [] }>()

type MenuKey = 'biz' | 'res' | 'api' | 'log' | 'sys' | 'user'
const menu = ref<MenuKey | null>(null)
const me = computed(() => session.me!)
const currentBiz = computed(() => (route.page === 'templates' && route.biz !== 'all' ? businessOf(route.biz) : null))
const scopeText = computed(() => (me.value.allBusinesses ? '全部业务' : me.value.businessIds.map((id) => businessOf(id).name).join('、') || '无'))

function toggle(key: MenuKey): void {
  menu.value = menu.value === key ? null : key
}

function open(page: Page, section = ''): void {
  menu.value = null
  go(page, section)
}

function openBiz(biz: number | 'all'): void {
  menu.value = null
  goBiz(biz)
}

function showShortcuts(): void {
  menu.value = null
  designer.ui.shortcutsOpen = true
}

async function logout(): Promise<void> {
  menu.value = null
  await signOut()
}

const close = () => (menu.value = null)
onMounted(() => document.addEventListener('click', close))
onBeforeUnmount(() => document.removeEventListener('click', close))
</script>

<template>
  <header class="topbar">
    <button class="hamburger" type="button" aria-label="打开模板树" @click.stop="$emit('toggleNav')"><AppIcon name="menu" /></button>
    <button class="brand" type="button" @click="open('dashboard')"><AppIcon name="brand" /><b>标签服务</b></button>
    <span class="brand-sub">标签设计与生成工作台</span>
    <span class="header-divider"></span>
    <div class="menu-wrap biz-wrap">
      <button class="project-select" type="button" aria-haspopup="menu" :aria-expanded="menu === 'biz'" @click.stop="toggle('biz')">
        {{ currentBiz ? currentBiz.name : '全部业务' }}<AppIcon name="chevD" small />
      </button>
      <div v-if="menu === 'biz'" class="dropdown left" role="menu" @click.stop>
        <h4>切换业务<span>{{ catalog.businesses.length }} 个</span></h4>
        <button class="dd-item" type="button" :class="{ on: route.page === 'templates' && route.biz === 'all' }" @click="openBiz('all')"><AppIcon name="folder" />全部业务<span class="count">{{ catalog.templates.length }}</span></button>
        <button v-for="b in catalog.businesses" :key="b.id" class="dd-item" type="button" :class="{ on: route.biz === b.id }" @click="openBiz(b.id)">
          <span class="biz-dot" :style="{ background: b.color }"></span>{{ b.name }}<span class="count">{{ templateCountByBusiness[b.id] || 0 }}</span>
        </button>
        <small v-if="can('biz.manage')"><button class="link" type="button" @click="open('business')">管理业务分组</button></small>
      </div>
    </div>
    <nav class="header-actions" aria-label="功能菜单">
      <button class="top-btn wide-only" type="button" :class="{ on: route.page === 'dashboard' }" @click="open('dashboard')"><AppIcon name="dashboard" /><span class="lbl">工作台</span></button>
      <div class="menu-wrap wide-only">
        <button class="top-btn" type="button" :class="{ on: menu === 'res' || route.page === 'presets' || route.page === 'assets' }" @click.stop="toggle('res')">
          <AppIcon name="ruler" /><span class="lbl">资源</span><AppIcon name="chevD" small />
        </button>
        <div v-if="menu === 'res'" class="dropdown" role="menu" @click.stop>
          <h4>设计资源<span>所有业务共用</span></h4>
          <button class="dd-item" type="button" @click="open('presets')"><AppIcon name="ruler" />画布预设<span class="count">{{ catalog.presets.length }}</span></button>
          <button class="dd-item" type="button" @click="open('assets')"><AppIcon name="image" />素材与字体</button>
        </div>
      </div>
      <div v-if="can('api.test') || can('api.manage')" class="menu-wrap wide-only">
        <button class="top-btn" type="button" :class="{ on: menu === 'api' || route.page === 'clients' || route.page === 'apitest' }" @click.stop="toggle('api')">
          <AppIcon name="api" /><span class="lbl">接口</span><AppIcon name="chevD" small />
        </button>
        <div v-if="menu === 'api'" class="dropdown" role="menu" @click.stop>
          <h4>开放接口<span>/api/v1</span></h4>
          <button v-if="can('api.manage')" class="dd-item" type="button" @click="open('clients')"><AppIcon name="key" />调用方与密钥</button>
          <button v-if="can('api.test')" class="dd-item" type="button" @click="open('apitest')"><AppIcon name="send" />接口调试</button>
          <small>业务系统在请求头带 X-Api-Key 调用，返回 PDF、PNG 或 ZPL 文件流。</small>
        </div>
      </div>
      <div v-if="can('log.view')" class="menu-wrap wide-only">
        <button class="top-btn" type="button" :class="{ on: menu === 'log' || route.page === 'calllogs' || route.page === 'oplogs' }" @click.stop="toggle('log')">
          <AppIcon name="logs" /><span class="lbl">日志</span><AppIcon name="chevD" small />
        </button>
        <div v-if="menu === 'log'" class="dropdown" role="menu" @click.stop>
          <h4>日志管理<span>保留 180 天</span></h4>
          <button class="dd-item" type="button" @click="open('calllogs')"><AppIcon name="activity" />调用日志</button>
          <button class="dd-item" type="button" @click="open('oplogs')"><AppIcon name="logs" />操作日志</button>
        </div>
      </div>
      <div v-if="can('sys.user') || can('sys.role')" class="menu-wrap wide-only">
        <button class="top-btn" type="button" :class="{ on: menu === 'sys' || route.page === 'users' || route.page === 'roles' }" @click.stop="toggle('sys')">
          <AppIcon name="shield" /><span class="lbl">权限管理</span><AppIcon name="chevD" small />
        </button>
        <div v-if="menu === 'sys'" class="dropdown" role="menu" @click.stop>
          <h4>权限管理<span>RBAC</span></h4>
          <button v-if="can('sys.user')" class="dd-item" type="button" @click="open('users')"><AppIcon name="users" />用户管理</button>
          <button v-if="can('sys.role')" class="dd-item" type="button" @click="open('roles')"><AppIcon name="shield" />角色权限<span class="count">{{ catalog.roles.length }}</span></button>
        </div>
      </div>
      <div class="menu-wrap">
        <button class="top-btn" type="button" aria-haspopup="menu" :aria-expanded="menu === 'user'" @click.stop="toggle('user')">
          <span class="avatar">{{ me.name.slice(0, 1) }}</span><span class="user-name">{{ me.name }}</span><AppIcon name="chevD" small />
        </button>
        <div v-if="menu === 'user'" class="dropdown" role="menu" @click.stop>
          <h4>{{ me.name }}<span>{{ me.role.name }}</span></h4>
          <div class="hint" style="padding: 0 9px 6px">{{ me.account }} · 业务范围：{{ scopeText }}</div>
          <button class="dd-item" type="button" @click="open('profile')"><AppIcon name="user" />个人设置</button>
          <button class="dd-item" type="button" @click="open('profile', 'pwd')"><AppIcon name="lock" />修改密码</button>
          <button v-if="route.page === 'workspace' && route.view === 'designer'" class="dd-item" type="button" @click="showShortcuts"><AppIcon name="keyboard" />快捷键</button>
          <button class="dd-item danger" type="button" @click="logout"><AppIcon name="logout" />退出登录</button>
        </div>
      </div>
    </nav>
  </header>
</template>

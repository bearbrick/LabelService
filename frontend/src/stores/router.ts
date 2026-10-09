/**
 * 简单的 hash 路由，不引入 vue-router：地址形如 `#/templates/3`、`#/t/BOX_LABEL/fields`、`#/calllogs`、`#/layouts/A4_100x60_2x4`。
 * 页面切换只改地址，route 跟着 hashchange 更新；刷新页面停在原来的位置。
 */
import { reactive } from 'vue'

export const PAGES = [
  'dashboard',
  'templates',
  'workspace',
  'business',
  'presets',
  'layouts',
  'assets',
  'clients',
  'apitest',
  'calllogs',
  'oplogs',
  'users',
  'roles',
  'profile',
] as const
export type Page = (typeof PAGES)[number]

export const WORKSPACE_VIEWS = ['designer', 'fields', 'api', 'versions'] as const
export type WorkspaceView = (typeof WORKSPACE_VIEWS)[number]

export interface Route {
  page: Page
  /** 模板库的业务分组，all 是全部。 */
  biz: number | 'all'
  /** 工作区打开的模板编码。 */
  code: string
  view: WorkspaceView
  /** 页面内的小节（如个人设置的 pwd）。 */
  section: string
}

export const route = reactive<Route>(parse(location.hash))

/** 解析地址。不认识的地址回到工作台。 */
export function parse(hash: string): Route {
  const parts = hash.replace(/^#\/?/, '').split('/').map(decodeURIComponent)
  const base: Route = { page: 'dashboard', biz: 'all', code: '', view: 'designer', section: '' }
  const [head, a, b] = parts
  if (head === 't' && a) {
    const view = (WORKSPACE_VIEWS as readonly string[]).includes(b) ? (b as WorkspaceView) : 'designer'
    return { ...base, page: 'workspace', code: a, view }
  }

  if (head === 'templates') return { ...base, page: 'templates', biz: a && /^\d+$/.test(a) ? Number(a) : 'all' }
  if ((PAGES as readonly string[]).includes(head) && head !== 'workspace') return { ...base, page: head as Page, section: a ?? '' }
  return base
}

/** 生成地址。 */
export function href(target: Partial<Route> & { page: Page }): string {
  switch (target.page) {
    case 'workspace':
      return `#/t/${encodeURIComponent(target.code ?? '')}/${target.view ?? 'designer'}`
    case 'templates':
      return target.biz && target.biz !== 'all' ? `#/templates/${target.biz}` : '#/templates'
    default:
      return `#/${target.page}${target.section ? `/${target.section}` : ''}`
  }
}

export function go(page: Page, section = ''): void {
  navigate(href({ page, section }))
}

export function goBiz(biz: number | 'all'): void {
  navigate(href({ page: 'templates', biz }))
}

export function openWorkspace(code: string, view: WorkspaceView = 'designer'): void {
  navigate(href({ page: 'workspace', code, view }))
}

function navigate(hash: string): void {
  if (location.hash === hash) return
  location.hash = hash
}

window.addEventListener('hashchange', () => Object.assign(route, parse(location.hash)))

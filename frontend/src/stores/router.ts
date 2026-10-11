/**
 * 简单的 hash 路由，不引入 vue-router：地址形如 `#/templates/3`、`#/t/BOX_LABEL/fields`、`#/calllogs`、`#/layouts/A4_100x60_2x4`。
 * 页面切换只改地址，route 跟着 hashchange 更新；刷新页面停在原来的位置。
 */
import { reactive } from 'vue'

/**
 * 全部页面（地址里的第一段，工作区除外）。
 */
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
/**
 * 页面名。
 */
export type Page = (typeof PAGES)[number]

/**
 * 模板工作区的页签：设计、数据源、接口调试、版本记录。
 */
export const WORKSPACE_VIEWS = ['designer', 'fields', 'api', 'versions'] as const
/**
 * 模板工作区的页签名。
 */
export type WorkspaceView = (typeof WORKSPACE_VIEWS)[number]

/**
 * 当前路由。
 */
export interface Route {
  /**
   * 当前页面。
   */
  page: Page
  /**
   * 模板库的业务分组，all 是全部。
   */
  biz: number | 'all'
  /**
   * 工作区打开的模板编码。
   */
  code: string
  /**
   * 工作区当前的页签。
   */
  view: WorkspaceView
  /**
   * 页面内的小节（如个人设置的 pwd）。
   */
  section: string
}

/**
 * 当前路由（响应式，hashchange 时更新）。
 */
export const route = reactive<Route>(parse(location.hash))

/**
 * 解析地址。不认识的地址回到工作台。
 * @param hash location.hash。
 * @returns 路由。
 */
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

/**
 * 生成地址。
 * @param target 目标页面和参数。
 * @returns 以 #/ 开头的地址。
 */
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

/**
 * 打开一个页面。
 * @param page 页面。
 * @param section 页面内的小节。
 */
export function go(page: Page, section = ''): void {
  navigate(href({ page, section }))
}

/**
 * 打开模板库的某个业务分组。
 * @param biz 分组 ID；all 是全部。
 */
export function goBiz(biz: number | 'all'): void {
  navigate(href({ page: 'templates', biz }))
}

/**
 * 打开模板工作区。
 * @param code 模板编码。
 * @param view 页签，默认设计。
 */
export function openWorkspace(code: string, view: WorkspaceView = 'designer'): void {
  navigate(href({ page: 'workspace', code, view }))
}

/**
 * 改地址（相同时不改，避免多一条历史记录）。
 * @param hash 新地址。
 */
function navigate(hash: string): void {
  if (location.hash === hash) return
  location.hash = hash
}

window.addEventListener('hashchange', () => Object.assign(route, parse(location.hash)))

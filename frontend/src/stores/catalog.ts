/**
 * 后台各页共用的列表：业务分组、模板、画布预设、角色。登录后加载一次，增删改后调对应的 reload。
 */
import { computed, reactive } from 'vue'
import * as adminApi from '../api/admin.ts'
import type { Business, Preset, Role, TemplateItem } from '../api/admin.ts'
import { can } from './session.ts'

/**
 * 后台共用的列表。
 */
export const catalog = reactive({
  /**
   * 自己范围里的业务分组。
   */
  businesses: [] as Business[],
  /**
   * 自己范围里的模板（没有 tpl.view 时为空）。
   */
  templates: [] as TemplateItem[],
  /**
   * 画布预设。
   */
  presets: [] as Preset[],
  /**
   * 角色。
   */
  roles: [] as Role[],
  /**
   * 登录后是否已加载过。
   */
  loaded: false,
})

/**
 * 登录后加载全部列表。
 * @returns 加载完成时结束。
 */
export async function loadCatalog(): Promise<void> {
  await Promise.all([reloadBusinesses(), reloadTemplates(), reloadPresets(), reloadRoles()])
  catalog.loaded = true
}

/**
 * 重新读取业务分组。
 * @returns 读取完成时结束。
 */
export async function reloadBusinesses(): Promise<void> {
  catalog.businesses = await adminApi.listBusinesses()
}

/**
 * 重新读取模板列表（没有 tpl.view 时清空）。
 * @returns 读取完成时结束。
 */
export async function reloadTemplates(): Promise<void> {
  catalog.templates = can('tpl.view') ? await adminApi.listTemplates() : []
}

/**
 * 重新读取画布预设。
 * @returns 读取完成时结束。
 */
export async function reloadPresets(): Promise<void> {
  catalog.presets = await adminApi.listPresets()
}

/**
 * 重新读取角色。
 * @returns 读取完成时结束。
 */
export async function reloadRoles(): Promise<void> {
  catalog.roles = await adminApi.listRoles()
}

/**
 * 退出登录时清空全部列表。
 */
export function clearCatalog(): void {
  catalog.businesses = []
  catalog.templates = []
  catalog.presets = []
  catalog.roles = []
  catalog.loaded = false
}

/**
 * 没有分组（或分组不在自己范围里）的模板显示成“未分组”。
 */
const UNGROUPED: Business = { id: 0, code: '-', name: '未分组', description: '', ownerUserId: null, color: '#a0a8b8', templates: 0 }

/**
 * 按 ID 找业务分组。
 * @param id 分组 ID。
 * @returns 分组；找不到时是“未分组”。
 */
export function businessOf(id: number | null | undefined): Business {
  return catalog.businesses.find((item) => item.id === id) ?? UNGROUPED
}

/**
 * 按 ID 取角色名称。
 * @param id 角色 ID。
 * @returns 角色名称；找不到时为 —。
 */
export function roleName(id: number): string {
  return catalog.roles.find((role) => role.id === id)?.name ?? '—'
}

/**
 * 每个业务分组下的模板数。
 */
export const templateCountByBusiness = computed(() => {
  const counts: Record<number, number> = {}
  for (const template of catalog.templates) {
    const id = template.businessId ?? 0
    counts[id] = (counts[id] ?? 0) + 1
  }

  return counts
})

/**
 * 模板状态：草稿（从没发布）、已发布、已发布但有未发布的改动。
 */
export type TemplateState = 'draft' | 'changed' | 'published'

/**
 * 模板状态的显示：键、短文字（卡片）、完整文字、样式类。
 * @param template 模板（只用最新版本号和有没有草稿）。
 * @returns 显示信息。
 */
export function templateState(template: Pick<TemplateItem, 'latestVersion' | 'hasDraft'>): { key: TemplateState; short: string; text: string; cls: string } {
  if (template.latestVersion === 0) return { key: 'draft', short: '未发布', text: '草稿 · 未发布', cls: 'warn' }
  if (template.hasDraft) return { key: 'changed', short: `v${template.latestVersion} · 有改动`, text: `v${template.latestVersion} 已发布 · 草稿有改动`, cls: 'accent' }
  return { key: 'published', short: `v${template.latestVersion} 已发布`, text: `v${template.latestVersion} 已发布`, cls: 'ok' }
}

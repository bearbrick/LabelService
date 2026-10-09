/**
 * 后台各页共用的列表：业务分组、模板、画布预设、角色。登录后加载一次，增删改后调对应的 reload。
 */
import { computed, reactive } from 'vue'
import * as adminApi from '../api/admin.ts'
import type { Business, Preset, Role, TemplateItem } from '../api/admin.ts'
import { can } from './session.ts'

export const catalog = reactive({
  businesses: [] as Business[],
  templates: [] as TemplateItem[],
  presets: [] as Preset[],
  roles: [] as Role[],
  loaded: false,
})

export async function loadCatalog(): Promise<void> {
  await Promise.all([reloadBusinesses(), reloadTemplates(), reloadPresets(), reloadRoles()])
  catalog.loaded = true
}

export async function reloadBusinesses(): Promise<void> {
  catalog.businesses = await adminApi.listBusinesses()
}

export async function reloadTemplates(): Promise<void> {
  catalog.templates = can('tpl.view') ? await adminApi.listTemplates() : []
}

export async function reloadPresets(): Promise<void> {
  catalog.presets = await adminApi.listPresets()
}

export async function reloadRoles(): Promise<void> {
  catalog.roles = await adminApi.listRoles()
}

export function clearCatalog(): void {
  catalog.businesses = []
  catalog.templates = []
  catalog.presets = []
  catalog.roles = []
  catalog.loaded = false
}

const UNGROUPED: Business = { id: 0, code: '-', name: '未分组', description: '', ownerUserId: null, color: '#a0a8b8', templates: 0 }

export function businessOf(id: number | null | undefined): Business {
  return catalog.businesses.find((item) => item.id === id) ?? UNGROUPED
}

export function roleName(id: number): string {
  return catalog.roles.find((role) => role.id === id)?.name ?? '—'
}

/** 每个业务分组下的模板数。 */
export const templateCountByBusiness = computed(() => {
  const counts: Record<number, number> = {}
  for (const template of catalog.templates) {
    const id = template.businessId ?? 0
    counts[id] = (counts[id] ?? 0) + 1
  }

  return counts
})

/** 模板状态：草稿（从没发布）、已发布、已发布但有未发布的改动。 */
export type TemplateState = 'draft' | 'changed' | 'published'

export function templateState(template: Pick<TemplateItem, 'latestVersion' | 'hasDraft'>): { key: TemplateState; short: string; text: string; cls: string } {
  if (template.latestVersion === 0) return { key: 'draft', short: '未发布', text: '草稿 · 未发布', cls: 'warn' }
  if (template.hasDraft) return { key: 'changed', short: `v${template.latestVersion} · 有改动`, text: `v${template.latestVersion} 已发布 · 草稿有改动`, cls: 'accent' }
  return { key: 'published', short: `v${template.latestVersion} 已发布`, text: `v${template.latestVersion} 已发布`, cls: 'ok' }
}

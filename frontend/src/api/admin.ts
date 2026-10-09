/**
 * 管理接口的类型和调用函数，和 docs/admin-api.md 一一对应。时间都是 UTC 的 ISO 字符串。
 */
import type { LabelTemplate } from '../model/template.ts'
import { api, apiRaw, query } from './http.ts'

export interface Me {
  id: number
  account: string
  name: string
  phone: string
  role: { id: number; name: string }
  permissions: string[]
  allBusinesses: boolean
  businessIds: number[]
  mustChangePassword: boolean
  lastLoginAt: string | null
}

export interface PermissionInfo {
  key: string
  label: string
  group: string
}

export interface User {
  id: number
  account: string
  name: string
  phone: string
  roleId: number
  allBusinesses: boolean
  businessIds: number[]
  status: 'active' | 'disabled'
  mustChangePassword: boolean
  lastLoginAt: string | null
  createdAt: string
}

export interface Role {
  id: number
  name: string
  description: string
  allPermissions: boolean
  permissions: string[]
  builtin: boolean
  users: number
}

export interface Business {
  id: number
  code: string
  name: string
  description: string
  ownerUserId: number | null
  color: string
  templates: number
}

export type TemplateStatus = 'draft' | 'published'

export interface TemplateItem {
  id: number
  code: string
  name: string
  businessId: number | null
  status: TemplateStatus
  latestVersion: number
  hasDraft: boolean
  createdBy: string
  updatedBy: string
  updatedAt: string
  width: number
  height: number
  dpi: number
  fieldCount: number
  elementCount: number
}

export interface TemplateDetail {
  code: string
  name: string
  businessId: number | null
  status: TemplateStatus
  latestVersion: number
  hasDraft: boolean
  updatedBy: string
  updatedAt: string
  content: LabelTemplate
}

export interface VersionInfo {
  versionNo: number
  status: string
  publishedBy: string | null
  publishedAt: string | null
  note: string
}

export interface PublishCheck {
  errors: string[]
  warnings: string[]
  renderMs: number | null
  canPublish: boolean
}

export interface Preset {
  id: number
  name: string
  kind: string
  widthMm: number
  heightMm: number
  sort: number
  enabled: boolean
}

export interface AssetInfo {
  key: string
  name: string
  builtin: boolean
  size: number
  createdAt: string | null
}

export interface ApiClient {
  id: number
  name: string
  appId: string
  description: string
  keyPrefix: string
  allBusinesses: boolean
  businessIds: number[]
  ipAllowlist: string
  enabled: boolean
  createdAt: string
  updatedAt: string
}

export interface CallLog {
  id: number
  requestId: string
  traceId: string
  ip: string
  endpoint: string
  clientId: number | null
  clientName: string
  templateCode: string
  versionNo: number | null
  format: string
  layoutCode: string | null
  itemCount: number
  durationMs: number
  stageMsJson: string
  isSlow: boolean
  status: string
  errorCode: string | null
  errorMessage: string | null
  createdAt: string
}

export interface OpLog {
  id: number
  userId: number | null
  userName: string
  type: string
  target: string
  detail: string
  ip: string
  createdAt: string
}

export interface Dashboard {
  templates: { total: number; published: number; changed: number; neverPublished: number }
  today?: { total: number; errors: number; slow: number; labels: number; averageMs: number }
  days?: { date: string; total: number; errors: number }[]
  topTemplates?: { code: string; calls: number; labels: number }[]
  recentOps?: OpLog[]
}

/* ---------------- 账号 ---------------- */

export const login = (account: string, password: string, remember: boolean) =>
  api<{ token: string; expiresAt: string; me: Me }>('POST', '/login', { account, password, remember })
export const logout = () => api<void>('POST', '/logout')
export const getMe = () => api<Me>('GET', '/me')
export const updateProfile = (name: string, phone: string) => api<Me>('PUT', '/me', { name, phone })
export const changePassword = (oldPassword: string, newPassword: string) => api<void>('POST', '/me/password', { oldPassword, newPassword })
export const listPermissions = () => api<PermissionInfo[]>('GET', '/permissions')

/* ---------------- 用户与角色 ---------------- */

export interface UserInput {
  account?: string
  name: string
  roleId: number
  allBusinesses: boolean
  businessIds: number[]
}

export const listUsers = () => api<User[]>('GET', '/users')
export const createUser = (input: UserInput) => api<{ user: User; temporaryPassword: string }>('POST', '/users', input)
export const updateUser = (id: number, input: UserInput) => api<User>('PUT', `/users/${id}`, input)
export const setUserStatus = (id: number, enabled: boolean) => api<User>('POST', `/users/${id}/status`, { enabled })
export const resetPassword = (id: number) => api<{ temporaryPassword: string }>('POST', `/users/${id}/reset-password`)

export interface RoleInput {
  name: string
  description: string
  permissions?: string[]
  copyFromRoleId?: number
}

export const listRoles = () => api<Role[]>('GET', '/roles')
export const createRole = (input: RoleInput) => api<Role>('POST', '/roles', input)
export const updateRole = (id: number, input: RoleInput) => api<Role>('PUT', `/roles/${id}`, input)
export const deleteRole = (id: number) => api<void>('DELETE', `/roles/${id}`)

/* ---------------- 业务分组 ---------------- */

export interface BusinessInput {
  code: string
  name: string
  description: string
  ownerUserId: number | null
  color: string
}

export const listBusinesses = () => api<Business[]>('GET', '/businesses')
export const createBusiness = (input: BusinessInput) => api<Business>('POST', '/businesses', input)
export const updateBusiness = (id: number, input: BusinessInput) => api<Business>('PUT', `/businesses/${id}`, input)
export const deleteBusiness = (id: number) => api<void>('DELETE', `/businesses/${id}`)

/* ---------------- 模板 ---------------- */

const tpl = (code: string) => `/templates/${encodeURIComponent(code)}`

export interface CreateTemplateInput {
  code: string
  name: string
  businessId: number
  width?: number
  height?: number
  dpi?: number
  content?: LabelTemplate
}

export const listTemplates = () => api<TemplateItem[]>('GET', '/templates')
export const createTemplate = (input: CreateTemplateInput) => api<{ code: string }>('POST', '/templates', input)
export const checkTemplate = (content: LabelTemplate) => api<PublishCheck>('POST', '/templates/check', content)
export const getTemplate = (code: string) => api<TemplateDetail>('GET', tpl(code))
export const deleteTemplate = (code: string) => api<void>('DELETE', tpl(code))
export const saveDraft = (content: LabelTemplate) => api<{ draftVersion: number; savedAt: string }>('PUT', `${tpl(content.code)}/draft`, content)
export const discardDraft = (code: string) => api<void>('DELETE', `${tpl(code)}/draft`)
export const publishTemplate = (code: string, note: string) => api<{ version: number; warnings: string[]; renderMs: number }>('POST', `${tpl(code)}/publish`, { note })
export const listVersions = (code: string) => api<VersionInfo[]>('GET', `${tpl(code)}/versions`)
export const getVersion = (code: string, version: number) => api<LabelTemplate>('GET', `${tpl(code)}/versions/${version}`)
export const restoreVersion = (code: string, version: number) => api<{ draftVersion: number }>('POST', `${tpl(code)}/versions/${version}/restore`)
export const moveTemplate = (code: string, businessId: number) => api<void>('PUT', `${tpl(code)}/business`, { businessId })
export const templateThumbnail = (code: string, stamp: string) => apiRaw('GET', `${tpl(code)}/thumbnail${query({ v: stamp })}`)

/* ---------------- 资源 ---------------- */

export interface PresetInput {
  name: string
  kind: string
  width: number
  height: number
  enabled: boolean
}

export const listPresets = () => api<Preset[]>('GET', '/presets')
export const createPreset = (input: PresetInput) => api<Preset>('POST', '/presets', input)
export const updatePreset = (id: number, input: PresetInput) => api<Preset>('PUT', `/presets/${id}`, input)
export const deletePreset = (id: number) => api<void>('DELETE', `/presets/${id}`)

export const listAssets = () => api<AssetInfo[]>('GET', '/assets')
export const assetFile = (key: string) => apiRaw('GET', `/assets/${encodeURIComponent(key)}/file`)
export function uploadAsset(file: File): Promise<AssetInfo> {
  const form = new FormData()
  form.append('file', file)
  return api<AssetInfo>('POST', '/assets', form)
}
export const renameAsset = (key: string, name: string) => api<AssetInfo>('PUT', `/assets/${encodeURIComponent(key)}`, { name })
export const deleteAsset = (key: string) => api<void>('DELETE', `/assets/${encodeURIComponent(key)}`)
export const listFonts = () => api<{ families: string[]; files: string[] }>('GET', '/fonts')

/* ---------------- 调用方 ---------------- */

export interface ClientInput {
  name: string
  appId: string
  description: string
  allBusinesses: boolean
  businessIds: number[]
  ipAllowlist: string
}

export const listClients = () => api<ApiClient[]>('GET', '/clients')
export const createClient = (input: ClientInput) => api<{ client: ApiClient; apiKey: string }>('POST', '/clients', input)
export const updateClient = (id: number, input: ClientInput) => api<ApiClient>('PUT', `/clients/${id}`, input)
export const setClientStatus = (id: number, enabled: boolean) => api<ApiClient>('POST', `/clients/${id}/status`, { enabled })
export const resetClientKey = (id: number) => api<{ client: ApiClient; apiKey: string }>('POST', `/clients/${id}/reset-key`)

/* ---------------- 日志、概览、接口调试 ---------------- */

export interface CallLogFilter {
  from?: string
  to?: string
  templateCode?: string
  clientId?: number
  status?: string
  q?: string
  beforeTime?: string
  beforeId?: number
  pageSize?: number
}

export const searchCallLogs = (filter: CallLogFilter) => api<CallLog[]>('GET', `/call-logs${query({ ...filter })}`)

export interface OpLogFilter {
  from?: string
  to?: string
  userId?: number
  type?: string
  q?: string
  beforeId?: number
  pageSize?: number
}

export const searchOpLogs = (filter: OpLogFilter) => api<OpLog[]>('GET', `/op-logs${query({ ...filter })}`)
export const getDashboard = () => api<Dashboard>('GET', '/dashboard')
export const testRender = (body: object) => apiRaw('POST', '/api-test/render', body)

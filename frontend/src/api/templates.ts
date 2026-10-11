/**
 * 模板的管理接口（docs/admin-api.md 的 /templates、/preview）：列表、新建、草稿、发布、版本、回滚、换分组、缩略图、服务端预览。
 */
import type { LabelTemplate } from '../model/template.ts'
import { api, apiRaw, query } from './http.ts'

/**
 * 模板状态：只有草稿（从未发布）或已发布。
 */
export type TemplateStatus = 'draft' | 'published'

/**
 * 模板库列表的一项（GET /templates）。
 */
export interface TemplateItem {
  /**
   * 模板 ID。
   */
  id: number
  /**
   * 模板编码。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 所属业务分组 ID；没有分组时为 null。
   */
  businessId: number | null
  /**
   * 状态。
   */
  status: TemplateStatus
  /**
   * 最新发布的版本号；从未发布时为 0。
   */
  latestVersion: number
  /**
   * 有没有未发布的草稿。
   */
  hasDraft: boolean
  /**
   * 创建人。
   */
  createdBy: string
  /**
   * 最后修改人。
   */
  updatedBy: string
  /**
   * 最后修改时间。
   */
  updatedAt: string
  /**
   * 画布宽度，mm。
   */
  width: number
  /**
   * 画布高度，mm。
   */
  height: number
  /**
   * 模板的分辨率。
   */
  dpi: number
  /**
   * 字段个数。
   */
  fieldCount: number
  /**
   * 元素个数。
   */
  elementCount: number
}

/**
 * 打开的模板（GET /templates/{code}）：有草稿给草稿，没有给最新发布版本。
 */
export interface TemplateDetail {
  /**
   * 模板编码。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 所属业务分组 ID；没有分组时为 null。
   */
  businessId: number | null
  /**
   * 状态。
   */
  status: TemplateStatus
  /**
   * 最新发布的版本号；从未发布时为 0。
   */
  latestVersion: number
  /**
   * content 是不是草稿。
   */
  hasDraft: boolean
  /**
   * 最后修改人。
   */
  updatedBy: string
  /**
   * 最后修改时间。
   */
  updatedAt: string
  /**
   * 模板 JSON。
   */
  content: LabelTemplate
}

/**
 * 版本记录的一项（GET /templates/{code}/versions）。
 */
export interface VersionInfo {
  /**
   * 版本号。
   */
  versionNo: number
  /**
   * 状态：draft 或 published。
   */
  status: string
  /**
   * 发布人；草稿为 null。
   */
  publishedBy: string | null
  /**
   * 发布时间；草稿为 null。
   */
  publishedAt: string | null
  /**
   * 发布说明。
   */
  note: string
}

/**
 * 发布前检查的结果（POST /templates/check）。
 */
export interface PublishCheck {
  /**
   * 不能发布的问题。
   */
  errors: string[]
  /**
   * 不影响发布的提醒（如字体降级）。
   */
  warnings: string[]
  /**
   * 试渲染耗时，毫秒；没有试渲染（有错误）时为 null。
   */
  renderMs: number | null
  /**
   * 能不能发布。
   */
  canPublish: boolean
}

/**
 * 新建模板的请求体：给尺寸建空白模板，或带 content（完整模板 JSON，如导入）。
 */
export interface CreateTemplateInput {
  /**
   * 模板编码（全局唯一，删除后也不能再用）。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 业务分组 ID。
   */
  businessId: number
  /**
   * 画布宽度，mm；带 content 时不用。
   */
  width?: number
  /**
   * 画布高度，mm；带 content 时不用。
   */
  height?: number
  /**
   * 分辨率；带 content 时不用。
   */
  dpi?: number
  /**
   * 完整的模板 JSON。
   */
  content?: LabelTemplate
}

/**
 * 服务端预览的请求体。
 */
export interface PreviewInput {
  /**
   * 模板 JSON（可以是还没保存的）。
   */
  template: LabelTemplate
  /**
   * 字段数据；不传时用字段的样例值。
   */
  data?: Record<string, unknown>
  /**
   * 分辨率；不传时用模板的。
   */
  dpi?: number
  /**
   * 黑白输出。
   */
  mono?: boolean
}

/**
 * 模板接口的路径前缀（编码做 URL 编码）。
 * @param code 模板编码。
 * @returns 如 /templates/BOX_LABEL。
 */
const tpl = (code: string) => `/templates/${encodeURIComponent(code)}`

/**
 * 取自己范围里的模板。
 * @returns 模板列表。
 */
export const listTemplates = () => api<TemplateItem[]>('GET', '/templates')

/**
 * 新建模板。
 * @param input 模板信息。
 * @returns 新模板的编码。
 */
export const createTemplate = (input: CreateTemplateInput) => api<{ code: string }>('POST', '/templates', input)

/**
 * 发布前检查加试渲染，不保存。
 * @param content 模板 JSON。
 * @returns 检查结果。
 */
export const checkTemplate = (content: LabelTemplate) => api<PublishCheck>('POST', '/templates/check', content)

/**
 * 打开模板。
 * @param code 模板编码。
 * @returns 模板详情（有草稿时是草稿）。
 */
export const getTemplate = (code: string) => api<TemplateDetail>('GET', tpl(code))

/**
 * 删除模板（软删除：开放接口立即不能用，编码不能再用）。
 * @param code 模板编码。
 * @returns 请求完成时结束。
 */
export const deleteTemplate = (code: string) => api<void>('DELETE', tpl(code))

/**
 * 保存草稿（编码不能改）。
 * @param content 模板 JSON。
 * @returns 草稿版本号和保存时间。
 */
export const saveDraft = (content: LabelTemplate) => api<{ draftVersion: number; savedAt: string }>('PUT', `${tpl(content.code)}/draft`, content)

/**
 * 放弃未发布的改动。
 * @param code 模板编码。
 * @returns 请求完成时结束。
 */
export const discardDraft = (code: string) => api<void>('DELETE', `${tpl(code)}/draft`)

/**
 * 发布草稿（检查不通过时 400，details 逐条列出）。
 * @param code 模板编码。
 * @param note 发布说明。
 * @returns 新版本号、提醒和试渲染耗时。
 */
export const publishTemplate = (code: string, note: string) => api<{ version: number; warnings: string[]; renderMs: number }>('POST', `${tpl(code)}/publish`, { note })

/**
 * 取版本记录。
 * @param code 模板编码。
 * @returns 全部版本，新的在前。
 */
export const listVersions = (code: string) => api<VersionInfo[]>('GET', `${tpl(code)}/versions`)

/**
 * 取某个已发布版本的模板 JSON。
 * @param code 模板编码。
 * @param version 版本号。
 * @returns 模板 JSON。
 */
export const getVersion = (code: string, version: number) => api<LabelTemplate>('GET', `${tpl(code)}/versions/${version}`)

/**
 * 回滚第一步：把某个版本复制成草稿。
 * @param code 模板编码。
 * @param version 版本号。
 * @returns 新草稿的版本号。
 */
export const restoreVersion = (code: string, version: number) => api<{ draftVersion: number }>('POST', `${tpl(code)}/versions/${version}/restore`)

/**
 * 把模板换到另一个业务分组（新旧分组都要在自己范围里）。
 * @param code 模板编码。
 * @param businessId 新的业务分组 ID。
 * @returns 请求完成时结束。
 */
export const moveTemplate = (code: string, businessId: number) => api<void>('PUT', `${tpl(code)}/business`, { businessId })

/**
 * 取模板库的缩略图（约 360 像素宽的 PNG）。
 * @param code 模板编码。
 * @param stamp 缓存版本（一般是修改时间），变了才重新下载。
 * @returns 响应（调用方读成 blob）。
 */
export const templateThumbnail = (code: string, stamp: string) => apiRaw('GET', `${tpl(code)}/thumbnail${query({ v: stamp })}`)

/**
 * 服务端预览：未保存的模板 + 样例数据（不传 data 时用字段样例值）→ PNG。
 * @param body 模板、数据、分辨率、黑白。
 * @returns 响应（PNG，带 X-Label-Dpi、Server-Timing、X-Label-Warnings）。
 */
export const previewTemplate = (body: PreviewInput) => apiRaw('POST', '/preview', body)

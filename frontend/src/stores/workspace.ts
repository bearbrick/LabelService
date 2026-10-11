/**
 * 模板工作区：从服务端打开模板交给设计器，自动保存草稿（PUT /draft），发布、版本、回滚、放弃改动、换业务分组。
 * 设计器只认 designer.template；这里负责它和服务端之间的往返，以及模板库列表（catalog）的同步。
 */
import { reactive } from 'vue'
import * as adminApi from '../api/admin.ts'
import type { TemplateDetail, VersionInfo } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import { catalog, reloadTemplates } from './catalog.ts'
import { designer, openTemplate, saveDraft, setDraftSaver, toast } from './designer.ts'
import { can, inScope, session } from './session.ts'

/**
 * 模板的概要（详情去掉模板 JSON）。
 */
type Summary = Omit<TemplateDetail, 'content'>

/**
 * 模板工作区的状态。
 */
export const workspace = reactive({
  /**
   * 打开的模板编码。
   */
  code: '',
  /**
   * 打开的模板的概要；打开失败时为 null。
   */
  detail: null as Summary | null,
  /**
   * 已发布的版本，新的在前。
   */
  versions: [] as VersionInfo[],
  /**
   * 正在打开。
   */
  loading: false,
  /**
   * 打开失败的原因。
   */
  error: '',
})

/**
 * 打开请求的序号：连续切换模板时只认最后一次。
 */
let loadId = 0

/**
 * 打开模板。先把正在编辑的模板的改动存掉。
 * @param code 模板编码。
 * @returns 打开结束（成功或失败）时完成。
 */
export async function loadWorkspace(code: string): Promise<void> {
  const id = ++loadId
  await saveDraft()
  if (id !== loadId) return
  workspace.code = code
  workspace.loading = true
  workspace.error = ''
  try {
    const detail = await adminApi.getTemplate(code)
    if (id !== loadId) return
    const { content, ...summary } = detail
    workspace.detail = summary
    openTemplate(content, !canEdit())
    setDraftSaver(saveToServer)
    void loadVersions()
  } catch (error) {
    if (id !== loadId) return
    workspace.detail = null
    workspace.error = errorText(error)
  } finally {
    if (id === loadId) workspace.loading = false
  }
}

/**
 * 当前用户能不能编辑打开的模板（有 tpl.edit 且模板在自己的业务范围里）。
 * @returns 能编辑时为 true。
 */
export function canEdit(): boolean {
  return can('tpl.edit') && (!workspace.detail || inScope(workspace.detail.businessId))
}

/**
 * 保存草稿到服务端（设计器自动保存时调用），成功后同步概要和模板库列表。
 * @param template 模板 JSON。
 * @returns 保存完成时结束；失败时抛出接口的错误。
 */
async function saveToServer(template: Parameters<typeof adminApi.saveDraft>[0]): Promise<void> {
  const result = await adminApi.saveDraft(template)
  const detail = workspace.detail
  if (!detail || detail.code !== template.code) return
  detail.hasDraft = true
  detail.name = template.name
  detail.updatedAt = result.savedAt
  detail.updatedBy = session.me?.name ?? detail.updatedBy
  syncCatalog(detail)
}

/**
 * 把模板库列表里的这一项和工作区同步（不用整表重读）。
 * @param detail 模板概要。
 */
function syncCatalog(detail: Summary): void {
  const item = catalog.templates.find((t) => t.code === detail.code)
  if (!item) return
  Object.assign(item, {
    name: detail.name,
    businessId: detail.businessId,
    status: detail.status,
    latestVersion: detail.latestVersion,
    hasDraft: detail.hasDraft,
    updatedAt: detail.updatedAt,
    updatedBy: detail.updatedBy,
    width: designer.template.page.width,
    height: designer.template.page.height,
    dpi: designer.template.page.dpi ?? item.dpi,
    fieldCount: designer.template.fields.length,
    elementCount: designer.template.elements.length,
  })
}

/**
 * 读取版本记录（只留已发布的，新的在前）。
 * @returns 读取结束时完成（失败时提示，不抛）。
 */
export async function loadVersions(): Promise<void> {
  const code = workspace.code
  try {
    const versions = await adminApi.listVersions(code)
    if (code === workspace.code) workspace.versions = versions.filter((v) => v.status === 'published').sort((a, b) => b.versionNo - a.versionNo)
  } catch (error) {
    toast(`读取版本记录失败：${errorText(error)}`, true)
  }
}

/**
 * 发布前检查：先保存草稿，再让服务端检查并试渲染。
 * @returns 检查结果。
 */
export async function checkBeforePublish(): Promise<adminApi.PublishCheck> {
  if (!(await saveDraft())) throw new Error('草稿还没保存成功，先处理保存失败的问题')
  return adminApi.checkTemplate(JSON.parse(JSON.stringify(designer.template)))
}

/**
 * 发布草稿，然后刷新概要和版本记录。
 * @param note 发布说明。
 * @returns 新版本号、提醒和试渲染耗时。
 */
export async function publish(note: string): Promise<{ version: number; warnings: string[]; renderMs: number }> {
  const result = await adminApi.publishTemplate(workspace.code, note)
  await reloadSummary()
  await loadVersions()
  return result
}

/**
 * 回滚：把某个版本复制成草稿，再重新打开。
 * @param version 版本号。
 * @returns 重新打开后完成。
 */
export async function restoreVersion(version: number): Promise<void> {
  await saveDraft()
  await adminApi.restoreVersion(workspace.code, version)
  await loadWorkspace(workspace.code)
  await reloadTemplates()
}

/**
 * 放弃未发布的改动，回到最新发布版本。
 * @returns 重新打开后完成。
 */
export async function discardDraft(): Promise<void> {
  await saveDraft()
  await adminApi.discardDraft(workspace.code)
  await loadWorkspace(workspace.code)
  await reloadTemplates()
}

/**
 * 把模板换到另一个业务分组。
 * @param businessId 新的业务分组 ID。
 * @returns 刷新完成时结束。
 */
export async function moveToBusiness(businessId: number): Promise<void> {
  await adminApi.moveTemplate(workspace.code, businessId)
  await reloadSummary()
  await reloadTemplates()
}

/**
 * 只重新读取概要（发布、换分组后），不打断设计器里的编辑。
 * @returns 读取完成时结束。
 */
async function reloadSummary(): Promise<void> {
  const { content: _content, ...summary } = await adminApi.getTemplate(workspace.code)
  workspace.detail = summary
  designer.readOnly = !canEdit()
  syncCatalog(summary)
}

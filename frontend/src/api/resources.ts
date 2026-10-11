/**
 * 资源的管理接口（docs/admin-api.md 的 /presets、/assets、/fonts）：画布预设、图片素材、字体。
 */
import { api, apiRaw } from './http.ts'

/**
 * 画布预设（GET /presets）。
 */
export interface Preset {
  /**
   * 预设 ID。
   */
  id: number
  /**
   * 名称，如“100×60 箱标”。
   */
  name: string
  /**
   * 类别，如“标签”“办公纸张”。
   */
  kind: string
  /**
   * 宽度，mm。
   */
  widthMm: number
  /**
   * 高度，mm。
   */
  heightMm: number
  /**
   * 排序号，小的在前。
   */
  sort: number
  /**
   * 是否在新建模板时可选。
   */
  enabled: boolean
}

/**
 * 新建或修改画布预设的请求体。
 */
export interface PresetInput {
  /**
   * 名称。
   */
  name: string
  /**
   * 类别。
   */
  kind: string
  /**
   * 宽度，mm。
   */
  width: number
  /**
   * 高度，mm。
   */
  height: number
  /**
   * 是否启用。
   */
  enabled: boolean
}

/**
 * 图片素材（GET /assets）；模板里写 asset:key 引用。
 */
export interface AssetInfo {
  /**
   * 素材 key。
   */
  key: string
  /**
   * 显示名称。
   */
  name: string
  /**
   * 内置素材（不能改、不能删）。
   */
  builtin: boolean
  /**
   * 文件大小，字节。
   */
  size: number
  /**
   * 上传时间；内置素材为 null。
   */
  createdAt: string | null
}

/**
 * 取画布预设。
 * @returns 全部预设。
 */
export const listPresets = () => api<Preset[]>('GET', '/presets')

/**
 * 新建画布预设。
 * @param input 预设信息。
 * @returns 新预设。
 */
export const createPreset = (input: PresetInput) => api<Preset>('POST', '/presets', input)

/**
 * 修改画布预设。
 * @param id 预设 ID。
 * @param input 预设信息。
 * @returns 修改后的预设。
 */
export const updatePreset = (id: number, input: PresetInput) => api<Preset>('PUT', `/presets/${id}`, input)

/**
 * 删除画布预设。
 * @param id 预设 ID。
 * @returns 请求完成时结束。
 */
export const deletePreset = (id: number) => api<void>('DELETE', `/presets/${id}`)

/**
 * 取图片素材（内置的和上传的）。
 * @returns 素材列表。
 */
export const listAssets = () => api<AssetInfo[]>('GET', '/assets')

/**
 * 下载素材文件（设计器显示用）。
 * @param key 素材 key。
 * @returns 响应（调用方读成 blob）。
 */
export const assetFile = (key: string) => apiRaw('GET', `/assets/${encodeURIComponent(key)}/file`)

/**
 * 上传素材：PNG、JPG、SVG，不超过 1 MB；服务端按内容判断类型，SVG 先清洗。
 * @param file 文件。
 * @returns 新素材。
 */
export function uploadAsset(file: File): Promise<AssetInfo> {
  const form = new FormData()
  form.append('file', file)
  return api<AssetInfo>('POST', '/assets', form)
}

/**
 * 素材改名（内置素材不能改）。
 * @param key 素材 key。
 * @param name 新名称。
 * @returns 修改后的素材。
 */
export const renameAsset = (key: string, name: string) => api<AssetInfo>('PUT', `/assets/${encodeURIComponent(key)}`, { name })

/**
 * 删除素材（内置素材不能删；引用它的模板生成时会报 IMAGE_INVALID）。
 * @param key 素材 key。
 * @returns 请求完成时结束。
 */
export const deleteAsset = (key: string) => api<void>('DELETE', `/assets/${encodeURIComponent(key)}`)

/**
 * 一个字体文件（GET /fonts 的 items）。
 */
export interface FontFile {
  /**
   * 文件名，删除时用它。
   */
  fileName: string
  /**
   * 文件里的字体家族名。
   */
  families: string[]
  /**
   * 文件大小，字节。
   */
  size: number
  /**
   * 文件修改时间（UTC），上传的字体就是上传时间。
   */
  lastModified: string
  /**
   * 是否在管理后台上传的（可以删除）；false 是部署目录里的字体（只读）。
   */
  uploaded: boolean
}

/**
 * 服务端的字体（GET /fonts）。
 */
export interface FontList {
  /**
   * 字体家族名（字体文件里的名字）。
   */
  families: string[]
  /**
   * 字体文件名。
   */
  files: string[]
  /**
   * 每个字体文件的详情。
   */
  items: FontFile[]
  /**
   * 模板里可以写的字体名（设计器的字体下拉框）：思源黑体、思源宋体用 SourceHanSansSC 等，其余用家族名。
   */
  templateFamilies: string[]
  /**
   * 有没有默认字体（思源黑体）。
   */
  hasDefaultFamily: boolean
}

/**
 * 取服务端加载的字体。
 * @returns 字体家族、文件和详情。
 */
export const listFonts = () => api<FontList>('GET', '/fonts')

/**
 * 上传字体（.ttf、.otf、.ttc、.otc，不超过 50 MB），立即生效（ADR 0011）。
 * @param file 字体文件。
 * @returns 上传后的字体列表。
 */
export function uploadFont(file: File): Promise<FontList> {
  const form = new FormData()
  form.append('file', file)
  return api<FontList>('POST', '/fonts', form)
}

/**
 * 删除上传的字体（部署目录里的不能删）；引用它的模板生成时降级到默认字体。
 * @param fileName 文件名。
 * @returns 删除后的字体列表。
 */
export const deleteFont = (fileName: string) => api<FontList>('DELETE', `/fonts/${encodeURIComponent(fileName)}`)

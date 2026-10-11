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
 * 取服务端加载的字体。
 * @returns 模板字体名和字体文件名。
 */
export const listFonts = () => api<{ families: string[]; files: string[] }>('GET', '/fonts')

/**
 * 系统信息的管理接口（docs/admin-api.md 的 /system-info）：系统名称、副标题、所属公司、版权信息、技术支持和公司 Logo。
 * 读取不用登录（登录页要显示），修改要权限点 sys.settings。
 */
import { api } from './http.ts'

/**
 * 系统信息（GET /system-info）。
 */
export interface SystemInfo {
  /**
   * 系统名称，不会为空；显示在顶栏和浏览器标签页上。
   */
  systemName: string
  /**
   * 副标题，显示在系统名称旁边；空表示不显示。
   */
  subtitle: string
  /**
   * 所属公司；空表示不显示。
   */
  companyName: string
  /**
   * 版权信息；空表示不显示。
   */
  copyright: string
  /**
   * 技术支持的联系方式；空表示不显示。
   */
  support: string
  /**
   * 登录页左栏的大标题（宣传标语），可以有换行（\n）。
   */
  loginTitle: string
  /**
   * 登录页大标题下面的说明，可以有换行；空表示不显示。
   */
  loginIntro: string
  /**
   * 公司 Logo 的版本，换一次 Logo 变一次；没上传时没有这个属性（界面显示占位）。
   */
  logoVersion?: string | null
  /**
   * Logo 图标（顶栏、浏览器标签页用）的版本；没上传时没有这个属性，界面改用完整 Logo。
   */
  iconVersion?: string | null
}

/**
 * 修改系统信息的文字部分（PUT /system-info）。
 */
export type SaveSystemInfo = Omit<SystemInfo, 'logoVersion' | 'iconVersion'>

/**
 * 每项最长多少字，和服务端的校验一致（Domain/SystemSettings/SystemInfoRules.cs）。
 */
export const SYSTEM_INFO_LIMITS: Record<keyof SaveSystemInfo, number> = {
  systemName: 20,
  subtitle: 30,
  companyName: 60,
  copyright: 120,
  support: 60,
  loginTitle: 40,
  loginIntro: 200,
}

/**
 * 没保存过时的默认值，接口还没读到时先用它显示。
 */
export const DEFAULT_SYSTEM_INFO: SystemInfo = {
  systemName: '标签服务',
  subtitle: '标签设计与生成工作台',
  companyName: '',
  copyright: '',
  support: '',
  loginTitle: '标签和唛头在网页上设计，\n业务系统一个接口生成',
  loginIntro: '拖拽设计模板，定义数据字段，发布后 MES、WMS、ERP 用模板编码加数据调用，拿回 PDF、PNG 或 ZPL 文件。',
}

/**
 * 读系统信息（不用登录）。
 * @returns 系统信息。
 */
export const getSystemInfo = () => api<SystemInfo>('GET', '/system-info')

/**
 * 修改系统信息的文字部分。
 * @param body 新的系统信息；每项会去掉首尾空白。
 * @returns 修改后的系统信息。
 */
export const saveSystemInfo = (body: SaveSystemInfo) => api<SystemInfo>('PUT', '/system-info', body)

/**
 * 上传完整的公司 Logo（登录页用）：PNG、JPG、SVG、WebP，不超过 1 MB；服务端按内容判断类型，SVG 先清洗。
 * @param file 图片文件。
 * @returns 修改后的系统信息（Logo 版本变了）。
 */
export function uploadLogo(file: File): Promise<SystemInfo> {
  const form = new FormData()
  form.append('file', file)
  return api<SystemInfo>('POST', '/system-info/logo', form)
}

/**
 * 删除上传的 Logo，恢复成占位。
 * @returns 修改后的系统信息。
 */
export const resetLogo = () => api<SystemInfo>('DELETE', '/system-info/logo')

/**
 * 上传 Logo 图标（顶栏、浏览器标签页用）：PNG、JPG、SVG、WebP，不超过 1 MB。
 * @param file 图片文件。
 * @returns 修改后的系统信息（图标版本变了）。
 */
export function uploadIcon(file: File): Promise<SystemInfo> {
  const form = new FormData()
  form.append('file', file)
  return api<SystemInfo>('POST', '/system-info/icon', form)
}

/**
 * 删除上传的 Logo 图标，顶栏和浏览器标签页改用完整 Logo。
 * @returns 修改后的系统信息。
 */
export const resetIcon = () => api<SystemInfo>('DELETE', '/system-info/icon')

/**
 * Logo 图标的图片地址（不用登录就能取）；没上传时为空字符串。
 * @param info 系统信息。
 * @returns 带版本参数的地址。
 */
export const iconUrl = (info: SystemInfo) => (info.iconVersion ? `/api/admin/system-info/icon?v=${encodeURIComponent(info.iconVersion)}` : '')

/**
 * 公司 Logo 的图片地址（不用登录就能取）；没上传时为空字符串。
 * @param info 系统信息。
 * @returns 带版本参数的地址，换了 Logo 地址就变，浏览器不会显示缓存里的旧图。
 */
export const logoUrl = (info: SystemInfo) => (info.logoVersion ? `/api/admin/system-info/logo?v=${encodeURIComponent(info.logoVersion)}` : '')

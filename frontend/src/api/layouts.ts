/**
 * 拼版版式的管理接口（docs/admin-api.md）：增删改、预览、打印校准测试页。
 */
import type { LayoutView, SheetLayout } from '../model/layout.ts'
import { api, apiRaw, query } from './http.ts'

/**
 * 版式接口的路径（编码做 URL 编码）。
 * @param code 版式编码。
 * @returns 如 /layouts/A4_100x60_2x4。
 */
const path = (code: string) => `/layouts/${encodeURIComponent(code)}`

/**
 * 预览用的一条数据。
 */
export interface PreviewItem {
  /**
   * 字段数据。
   */
  data: Record<string, unknown>
  /**
   * 份数，默认 1。
   */
  copies?: number
}

/**
 * 取拼版版式；带格子尺寸时只列格子尺寸相同的（模板详情页“可用的版式”）。
 * @param slotWidth 格子宽度，mm。
 * @param slotHeight 格子高度，mm。
 * @returns 版式列表。
 */
export const listLayouts = (slotWidth?: number, slotHeight?: number) => api<LayoutView[]>('GET', `/layouts${query({ slotWidth, slotHeight })}`)

/**
 * 取一个版式。
 * @param code 版式编码。
 * @returns 版式。
 */
export const getLayout = (code: string) => api<LayoutView>('GET', path(code))

/**
 * 新建版式；格子越界、重叠等问题时 400，details 逐条列出（带格子序号）。
 * @param layout 版式 JSON。
 * @param enabled 是否启用。
 * @returns 新版式。
 */
export const createLayout = (layout: SheetLayout, enabled = true) => api<LayoutView>('POST', '/layouts', { layout, enabled })

/**
 * 修改或停用版式（编码不能改），立即生效。
 * @param layout 版式 JSON。
 * @param enabled 是否启用；停用后开放接口返回 LAYOUT_NOT_FOUND。
 * @returns 修改后的版式。
 */
export const updateLayout = (layout: SheetLayout, enabled: boolean) => api<LayoutView>('PUT', path(layout.code), { layout, enabled })

/**
 * 删除版式。
 * @param code 版式编码。
 * @returns 请求完成时结束。
 */
export const deleteLayout = (code: string) => api<void>('DELETE', path(code))

/**
 * 预览还没保存的版式：不给 items 时用模板样例值铺满一页。PNG 多页时返回 ZIP。
 * @param input 版式、模板编码、格式、起始格子和数据。
 * @returns 响应（PNG、ZIP 或 PDF，带 X-Label-Count、X-Page-Count）。
 */
export const previewLayout = (input: { layout: SheetLayout; templateCode: string; format?: 'png' | 'pdf'; startSlot?: number; items?: PreviewItem[] }) =>
  apiRaw('POST', '/layouts/preview', input)

/**
 * 打印校准测试页：每个格子画边框、中心十字和序号。
 * @param layout 版式 JSON（含打印偏移）。
 * @param format 输出格式，默认 PDF。
 * @returns 响应（文件）。
 */
export const layoutTestPage = (layout: SheetLayout, format: 'pdf' | 'png' = 'pdf') => apiRaw('POST', '/layouts/test-page', { layout, format })

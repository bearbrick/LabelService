/**
 * 拼版版式的管理接口（docs/admin-api.md，T-018）：增删改、预览、打印校准测试页。
 */
import type { LayoutView, SheetLayout } from '../model/layout.ts'
import { api, apiRaw, query } from './http.ts'

const path = (code: string) => `/layouts/${encodeURIComponent(code)}`

/** 预览用的一条数据。 */
export interface PreviewItem {
  data: Record<string, unknown>
  copies?: number
}

export const listLayouts = (slotWidth?: number, slotHeight?: number) => api<LayoutView[]>('GET', `/layouts${query({ slotWidth, slotHeight })}`)
export const getLayout = (code: string) => api<LayoutView>('GET', path(code))
export const createLayout = (layout: SheetLayout, enabled = true) => api<LayoutView>('POST', '/layouts', { layout, enabled })
export const updateLayout = (layout: SheetLayout, enabled: boolean) => api<LayoutView>('PUT', path(layout.code), { layout, enabled })
export const deleteLayout = (code: string) => api<void>('DELETE', path(code))

/** 预览还没保存的版式：不给 items 时用模板样例值铺满一页。PNG 多页时返回 ZIP。 */
export const previewLayout = (input: { layout: SheetLayout; templateCode: string; format?: 'png' | 'pdf'; startSlot?: number; items?: PreviewItem[] }) =>
  apiRaw('POST', '/layouts/preview', input)

/** 打印校准测试页：每个格子画边框、中心十字和序号。 */
export const layoutTestPage = (layout: SheetLayout, format: 'pdf' | 'png' = 'pdf') => apiRaw('POST', '/layouts/test-page', { layout, format })

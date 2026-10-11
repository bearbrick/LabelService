/**
 * 日志、工作台和接口调试的管理接口（docs/admin-api.md 的 /call-logs、/op-logs、/dashboard、/api-test）。
 */
import { api, apiRaw, query } from './http.ts'

/**
 * 调用日志的一条（GET /call-logs）；不存业务数据。
 */
export interface CallLog {
  /**
   * 日志 ID。
   */
  id: number
  /**
   * 调用方传的 X-Request-Id；没传时服务端生成。
   */
  requestId: string
  /**
   * 服务端的跟踪 ID。
   */
  traceId: string
  /**
   * 来源 IP。
   */
  ip: string
  /**
   * 接口，如 render、render/batch。
   */
  endpoint: string
  /**
   * 调用方 ID；鉴权失败时为 null。
   */
  clientId: number | null
  /**
   * 调用方名称。
   */
  clientName: string
  /**
   * 模板编码。
   */
  templateCode: string
  /**
   * 用的模板版本；没找到模板时为 null。
   */
  versionNo: number | null
  /**
   * 输出格式。
   */
  format: string
  /**
   * 拼版用的版式编码；不拼版时为 null。
   */
  layoutCode: string | null
  /**
   * 标签张数（批量时份数加起来）。
   */
  itemCount: number
  /**
   * 总耗时，毫秒（从开始处理请求到写完最后一个字节）。
   */
  durationMs: number
  /**
   * 各阶段耗时的 JSON，如 {"auth":0.3,"draw":18.5}。
   */
  stageMsJson: string
  /**
   * 慢调用（单张超过 100 ms）。
   */
  isSlow: boolean
  /**
   * 结果：ok 或 error。
   */
  status: string
  /**
   * 错误码；成功时为 null。
   */
  errorCode: string | null
  /**
   * 错误说明；成功时为 null。
   */
  errorMessage: string | null
  /**
   * 调用时间。
   */
  createdAt: string
}

/**
 * 操作日志的一条（GET /op-logs）。
 */
export interface OpLog {
  /**
   * 日志 ID。
   */
  id: number
  /**
   * 操作人的用户 ID；系统操作为 null。
   */
  userId: number | null
  /**
   * 操作人。
   */
  userName: string
  /**
   * 操作类型，如“发布模板”。
   */
  type: string
  /**
   * 操作对象，如模板编码。
   */
  target: string
  /**
   * 详细说明。
   */
  detail: string
  /**
   * 来源 IP。
   */
  ip: string
  /**
   * 操作时间。
   */
  createdAt: string
}

/**
 * 工作台的统计（GET /dashboard）；有 log.view 权限时才有调用相关的部分。
 */
export interface Dashboard {
  /**
   * 模板数量：总数、已发布、有未发布改动、从未发布。
   */
  templates: { total: number; published: number; changed: number; neverPublished: number }
  /**
   * 今天的调用：次数、失败、慢调用、张数、平均耗时、单张成功调用的 P95。
   */
  today?: { total: number; errors: number; slow: number; labels: number; averageMs: number; p95Ms?: number | null }
  /**
   * 近 14 天每天的调用次数和失败次数。
   */
  days?: { date: string; total: number; errors: number }[]
  /**
   * 调用最多的模板。
   */
  topTemplates?: { code: string; calls: number; labels: number }[]
  /**
   * 近 7 天单张平均耗时最高的 5 个模板。
   */
  slowTemplates?: { code: string; calls: number; averageMs: number; maxMs: number }[]
  /**
   * 最近的操作。
   */
  recentOps?: OpLog[]
}

/**
 * 调用日志的筛选条件；翻页用上一页最后一条的 createdAt 和 id。
 */
export interface CallLogFilter {
  /**
   * 开始时间。
   */
  from?: string
  /**
   * 结束时间。
   */
  to?: string
  /**
   * 模板编码。
   */
  templateCode?: string
  /**
   * 调用方 ID。
   */
  clientId?: number
  /**
   * 结果：ok 或 error。
   */
  status?: string
  /**
   * 搜索：链路 ID、X-Request-Id、模板编码、错误码。
   */
  q?: string
  /**
   * 翻页：上一页最后一条的时间。
   */
  beforeTime?: string
  /**
   * 翻页：上一页最后一条的 ID。
   */
  beforeId?: number
  /**
   * 每页条数。
   */
  pageSize?: number
}

/**
 * 操作日志的筛选条件。
 */
export interface OpLogFilter {
  /**
   * 开始时间。
   */
  from?: string
  /**
   * 结束时间。
   */
  to?: string
  /**
   * 操作人的用户 ID。
   */
  userId?: number
  /**
   * 操作类型。
   */
  type?: string
  /**
   * 搜索：对象和说明。
   */
  q?: string
  /**
   * 翻页：上一页最后一条的 ID。
   */
  beforeId?: number
  /**
   * 每页条数。
   */
  pageSize?: number
}

/**
 * 查调用日志，按时间倒序。
 * @param filter 筛选条件。
 * @returns 一页日志。
 */
export const searchCallLogs = (filter: CallLogFilter) => api<CallLog[]>('GET', `/call-logs${query({ ...filter })}`)

/**
 * 查操作日志，按时间倒序。
 * @param filter 筛选条件。
 * @returns 一页日志。
 */
export const searchOpLogs = (filter: OpLogFilter) => api<OpLog[]>('GET', `/op-logs${query({ ...filter })}`)

/**
 * 取工作台的统计。
 * @returns 统计数据。
 */
export const getDashboard = () => api<Dashboard>('GET', '/dashboard')

/**
 * 接口调试：用后台登录试生成（不需要 Key，不写调用日志），请求体和开放接口单张生成一样。
 * @param body 请求体。
 * @returns 响应（成功时是文件）。
 */
export const testRender = (body: object) => apiRaw('POST', '/api-test/render', body)

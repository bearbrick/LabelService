/**
 * 调用方的管理接口（docs/admin-api.md 的 /clients）：一个接入的业务系统一把 API Key。
 */
import { api } from './http.ts'

/**
 * 调用方（GET /clients）；列表里只有 Key 前缀，没有 Key。
 */
export interface ApiClient {
  /**
   * 调用方 ID。
   */
  id: number
  /**
   * 名称（不能重复）。
   */
  name: string
  /**
   * 对方系统的应用标识（自由填写，方便对照）。
   */
  appId: string
  /**
   * 说明。
   */
  description: string
  /**
   * Key 的前几位，用来辨认是哪一把。
   */
  keyPrefix: string
  /**
   * 能调用全部业务分组的模板。
   */
  allBusinesses: boolean
  /**
   * 能调用的业务分组 ID。
   */
  businessIds: number[]
  /**
   * IP 白名单（逗号或换行分隔的地址和网段）；空表示不限制。
   */
  ipAllowlist: string
  /**
   * 是否启用；停用立即生效。
   */
  enabled: boolean
  /**
   * 创建时间。
   */
  createdAt: string
  /**
   * 最后修改时间。
   */
  updatedAt: string
}

/**
 * 新建或修改调用方的请求体。
 */
export interface ClientInput {
  /**
   * 名称。
   */
  name: string
  /**
   * 对方系统的应用标识。
   */
  appId: string
  /**
   * 说明。
   */
  description: string
  /**
   * 授权全部业务分组（业务范围受限的用户不能选）。
   */
  allBusinesses: boolean
  /**
   * 授权的业务分组 ID（只能选自己范围里的）。
   */
  businessIds: number[]
  /**
   * IP 白名单；空表示不限制。
   */
  ipAllowlist: string
}

/**
 * 取调用方列表（业务范围受限的用户只看到授权完全在自己范围里的）。
 * @returns 调用方列表。
 */
export const listClients = () => api<ApiClient[]>('GET', '/clients')

/**
 * 新建调用方。
 * @param input 调用方信息。
 * @returns 新调用方和它的 Key（Key 只返回这一次）。
 */
export const createClient = (input: ClientInput) => api<{ client: ApiClient; apiKey: string }>('POST', '/clients', input)

/**
 * 修改调用方（不改 Key）。
 * @param id 调用方 ID。
 * @param input 调用方信息。
 * @returns 修改后的调用方。
 */
export const updateClient = (id: number, input: ClientInput) => api<ApiClient>('PUT', `/clients/${id}`, input)

/**
 * 启用或停用调用方，立即生效。
 * @param id 调用方 ID。
 * @param enabled true 启用，false 停用。
 * @returns 修改后的调用方。
 */
export const setClientStatus = (id: number, enabled: boolean) => api<ApiClient>('POST', `/clients/${id}/status`, { enabled })

/**
 * 重置 Key，旧 Key 立即失效。
 * @param id 调用方 ID。
 * @returns 调用方和新 Key（只返回这一次）。
 */
export const resetClientKey = (id: number) => api<{ client: ApiClient; apiKey: string }>('POST', `/clients/${id}/reset-key`)

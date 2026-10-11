/**
 * 业务分组的管理接口（docs/admin-api.md 的 /businesses）。
 */
import { api } from './http.ts'

/**
 * 业务分组（GET /businesses）。
 */
export interface Business {
  /**
   * 分组 ID。
   */
  id: number
  /**
   * 分组编码（大写）。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 说明。
   */
  description: string
  /**
   * 负责人的用户 ID；没有指定时为 null。
   */
  ownerUserId: number | null
  /**
   * 标识色，如 #00939A，模板树和标签上用。
   */
  color: string
  /**
   * 分组里的模板数量。
   */
  templates: number
}

/**
 * 新建或修改业务分组的请求体。
 */
export interface BusinessInput {
  /**
   * 分组编码（服务端转大写，不能重复）。
   */
  code: string
  /**
   * 名称。
   */
  name: string
  /**
   * 说明。
   */
  description: string
  /**
   * 负责人的用户 ID；不指定时为 null。
   */
  ownerUserId: number | null
  /**
   * 标识色。
   */
  color: string
}

/**
 * 取自己范围里的业务分组。
 * @returns 业务分组列表。
 */
export const listBusinesses = () => api<Business[]>('GET', '/businesses')

/**
 * 新建业务分组。
 * @param input 分组信息。
 * @returns 新分组。
 */
export const createBusiness = (input: BusinessInput) => api<Business>('POST', '/businesses', input)

/**
 * 修改业务分组。
 * @param id 分组 ID。
 * @param input 分组信息。
 * @returns 修改后的分组。
 */
export const updateBusiness = (id: number, input: BusinessInput) => api<Business>('PUT', `/businesses/${id}`, input)

/**
 * 删除业务分组（分组里还有模板时不能删）。
 * @param id 分组 ID。
 * @returns 请求完成时结束。
 */
export const deleteBusiness = (id: number) => api<void>('DELETE', `/businesses/${id}`)

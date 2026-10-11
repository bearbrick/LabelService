/**
 * 管理接口（/api/admin）的请求封装：带登录令牌、统一解析错误。
 * 令牌勾选“7 天内自动登录”时存 localStorage，否则存 sessionStorage（关掉浏览器就要重新登录）。
 */

/**
 * 存登录令牌的键名（localStorage 或 sessionStorage）。
 */
const TOKEN_KEY = 'label-admin.token'
/**
 * 管理接口的路径前缀。
 */
const BASE = '/api/admin'

/**
 * 管理接口返回的错误：`{ code, message, details }`。
 */
export class ApiError extends Error {
  /**
   * HTTP 状态码；连不上服务端时为 0。
   */
  readonly status: number
  /**
   * 错误码，如 INVALID、FORBIDDEN；连不上时为 NETWORK。
   */
  readonly code: string
  /**
   * 逐条的问题（如发布前检查的错误）；没有时为空。
   */
  readonly details: string[]

  /**
   * 创建错误。
   * @param status HTTP 状态码。
   * @param code 错误码。
   * @param message 给用户看的说明。
   * @param details 逐条的问题。
   */
  constructor(status: number, code: string, message: string, details: string[] = []) {
    super(message)
    this.status = status
    this.code = code
    this.details = details
  }
}

/**
 * 当前的登录令牌；没登录时为 null。
 */
let token: string | null = readToken()
/**
 * 登录失效时的处理函数（由 session 注册）。
 */
let onUnauthorized: () => void = () => {}

/**
 * 从浏览器存储里读令牌（先看自动登录的 localStorage）。
 * @returns 令牌；没有或浏览器禁用了存储时为 null。
 */
function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

/**
 * 保存或清除令牌。
 * @param value 令牌；null 表示清除。
 * @param remember 勾选了“7 天内自动登录”：存 localStorage，否则存 sessionStorage。
 */
export function setToken(value: string | null, remember = false): void {
  token = value
  try {
    localStorage.removeItem(TOKEN_KEY)
    sessionStorage.removeItem(TOKEN_KEY)
    if (value) (remember ? localStorage : sessionStorage).setItem(TOKEN_KEY, value)
  } catch {
    // 浏览器禁用了存储：这次会话里仍然有效。
  }
}

/**
 * 是否有登录令牌（不保证还有效）。
 * @returns 有令牌时为 true。
 */
export function hasToken(): boolean {
  return !!token
}

/**
 * 登录失效（401 UNAUTHORIZED）时的处理，由 session 注册：清掉登录状态回到登录页。
 * @param handler 处理函数。
 */
export function handleUnauthorized(handler: () => void): void {
  onUnauthorized = handler
}

/**
 * 请求体：JSON 对象、上传文件的表单，或没有。
 */
type Body = object | FormData | undefined

/**
 * 发请求：带令牌，JSON 请求体加 Content-Type；失败时抛 ApiError。
 * @param method HTTP 方法。
 * @param path 相对 /api/admin 的路径。
 * @param body 请求体。
 * @returns 成功的响应。
 */
async function send(method: string, path: string, body?: Body): Promise<Response> {
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`
  let payload: BodyInit | undefined
  if (body instanceof FormData) payload = body
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  let response: Response
  try {
    response = await fetch(BASE + path, { method, headers, body: payload })
  } catch {
    throw new ApiError(0, 'NETWORK', '连不上服务端，请检查网络或服务是否启动')
  }

  if (response.ok) return response
  throw await readError(response)
}

/**
 * 把失败的响应读成 ApiError；登录失效（401 UNAUTHORIZED）时通知 session 回到登录页。
 * @param response 失败的响应。
 * @returns 错误（这里只构造，不抛）。
 */
async function readError(response: Response): Promise<ApiError> {
  let error: ApiError
  try {
    const json = (await response.json()) as { code?: string; message?: string; details?: string[] }
    error = new ApiError(response.status, json.code ?? 'ERROR', json.message ?? `请求失败（${response.status}）`, json.details ?? [])
  } catch {
    error = new ApiError(response.status, 'ERROR', `请求失败（${response.status} ${response.statusText}）`)
  }

  if (error.status === 401 && error.code === 'UNAUTHORIZED') onUnauthorized()
  return error
}

/**
 * 发请求并解析 JSON；204 返回 undefined。
 * @typeParam T 响应 JSON 的类型。
 * @param method HTTP 方法。
 * @param path 相对 /api/admin 的路径。
 * @param body 请求体。
 * @returns 解析好的响应。
 */
export async function api<T>(method: string, path: string, body?: Body): Promise<T> {
  const response = await send(method, path, body)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/**
 * 发请求并返回原始响应（文件下载、要读响应头时用）。
 * @param method HTTP 方法。
 * @param path 相对 /api/admin 的路径。
 * @param body 请求体。
 * @returns 成功的响应。
 */
export function apiRaw(method: string, path: string, body?: Body): Promise<Response> {
  return send(method, path, body)
}

/**
 * 拼查询字符串，跳过空值。
 * @param params 参数名 → 值；undefined、null、空字符串不拼。
 * @returns 以 ? 开头的查询字符串；没有参数时为空字符串。
 */
export function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }

  const text = search.toString()
  return text ? `?${text}` : ''
}

/**
 * 把错误转成给用户看的一句话。
 * @param error 捕获到的错误。
 * @returns 错误说明；有逐条问题时带上第一条。
 */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) return error.details.length ? `${error.message}：${error.details[0]}` : error.message
  return error instanceof Error ? error.message : String(error)
}

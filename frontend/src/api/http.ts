/**
 * 管理接口（/api/admin）的请求封装：带登录令牌、统一解析错误。
 * 令牌勾选“7 天内自动登录”时存 localStorage，否则存 sessionStorage（关掉浏览器就要重新登录）。
 */

const TOKEN_KEY = 'label-admin.token'
const BASE = '/api/admin'

/** 管理接口返回的错误：`{ code, message, details }`。 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly details: string[]

  constructor(status: number, code: string, message: string, details: string[] = []) {
    super(message)
    this.status = status
    this.code = code
    this.details = details
  }
}

let token: string | null = readToken()
let onUnauthorized: () => void = () => {}

function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

/** 保存或清除令牌。 */
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

export function hasToken(): boolean {
  return !!token
}

/** 登录失效（401 UNAUTHORIZED）时的处理，由 session 注册：清掉登录状态回到登录页。 */
export function handleUnauthorized(handler: () => void): void {
  onUnauthorized = handler
}

type Body = object | FormData | undefined

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

/** 发请求并解析 JSON；204 返回 undefined。 */
export async function api<T>(method: string, path: string, body?: Body): Promise<T> {
  const response = await send(method, path, body)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** 发请求并返回原始响应（文件下载、要读响应头时用）。 */
export function apiRaw(method: string, path: string, body?: Body): Promise<Response> {
  return send(method, path, body)
}

/** 拼查询字符串，跳过空值。 */
export function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }

  const text = search.toString()
  return text ? `?${text}` : ''
}

/** 把错误转成给用户看的一句话。 */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) return error.details.length ? `${error.message}：${error.details[0]}` : error.message
  return error instanceof Error ? error.message : String(error)
}

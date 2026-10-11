/**
 * 登录状态：当前用户、权限点、业务范围。按钮的禁用和隐藏用这里的 can()、inScope()；服务端每个接口也会再校验。
 */
import { reactive } from 'vue'
import * as adminApi from '../api/admin.ts'
import type { Me } from '../api/admin.ts'
import { handleUnauthorized, hasToken, setToken } from '../api/http.ts'

export const session = reactive({
  me: null as Me | null,
  /**
   * 启动时检查完已保存的令牌后为 true。
   */
  ready: false,
  /**
   * 登录失效被踢回登录页时的提示。
   */
  notice: '',
})

handleUnauthorized(() => {
  if (session.me) session.notice = '登录已失效，请重新登录'
  clear()
})

function clear(): void {
  setToken(null)
  session.me = null
}

/**
 * 启动时用保存的令牌取当前用户；令牌无效时留在登录页。
 */
export async function restoreSession(): Promise<void> {
  try {
    if (hasToken()) session.me = await adminApi.getMe()
  } catch {
    clear()
  } finally {
    session.ready = true
  }
}

export async function signIn(account: string, password: string, remember: boolean): Promise<void> {
  const result = await adminApi.login(account, password, remember)
  setToken(result.token, remember)
  session.notice = ''
  session.me = result.me
}

export async function signOut(): Promise<void> {
  try {
    await adminApi.logout()
  } catch {
    // 令牌已经失效也照样退出。
  }

  clear()
}

/**
 * 重新读取当前用户（改了个人信息、密码，或管理员改了自己的权限后）。
 */
export async function refreshMe(): Promise<void> {
  session.me = await adminApi.getMe()
}

/**
 * 当前用户有没有某个权限点。必须先改密码时一律没有。
 */
export function can(permission: string): boolean {
  return !!session.me && !session.me.mustChangePassword && session.me.permissions.includes(permission)
}

/**
 * 业务分组在不在当前用户的范围里。
 */
export function inScope(businessId: number | null | undefined): boolean {
  const me = session.me
  if (!me) return false
  return me.allBusinesses || (businessId != null && me.businessIds.includes(businessId))
}

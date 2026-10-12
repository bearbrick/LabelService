/**
 * 最近打开的模板：按登录账号记在浏览器里（localStorage），模板树顶部显示。存不了时（隐私模式等）只在本次打开期间有效。
 */
import { reactive } from 'vue'
import { pushRecent, RECENT_LIMIT } from '../model/templateList.ts'

/**
 * 最近打开的模板。
 */
export const recentTemplates = reactive({
  /**
   * 模板编码，新的在前。
   */
  codes: [] as string[],
  /**
   * 这份列表属于哪个账号；换账号时重新读取。
   */
  account: '',
})

/**
 * 某个账号的存储键。
 * @param account 登录账号。
 * @returns 如 label-admin.recent-templates.admin。
 */
const storageKey = (account: string) => `label-admin.recent-templates.${account}`

/**
 * 读取某个账号的最近打开（换账号、刷新页面后调用）。
 * @param account 登录账号；空字符串表示没登录，清空列表。
 */
export function loadRecentTemplates(account: string): void {
  recentTemplates.account = account
  recentTemplates.codes = []
  if (!account) return
  try {
    const saved: unknown = JSON.parse(localStorage.getItem(storageKey(account)) ?? '[]')
    if (Array.isArray(saved)) recentTemplates.codes = saved.filter((code): code is string => typeof code === 'string').slice(0, RECENT_LIMIT)
  } catch {
    // 存储不可用或内容损坏：当作没有记录
  }
}

/**
 * 记一次打开模板。
 * @param code 模板编码。
 */
export function rememberTemplate(code: string): void {
  recentTemplates.codes = pushRecent(recentTemplates.codes, code, RECENT_LIMIT)
  if (!recentTemplates.account) return
  try {
    localStorage.setItem(storageKey(recentTemplates.account), JSON.stringify(recentTemplates.codes))
  } catch {
    // 存不了就只在本次打开期间有效
  }
}

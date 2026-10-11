/**
 * 系统信息（系统名称、公司、Logo、版权信息）：打开页面时读一次（登录页也要用），系统信息页保存后更新；浏览器标签页标题跟着系统名称变。
 */
import { reactive } from 'vue'
import { DEFAULT_SYSTEM_INFO, getSystemInfo } from '../api/system.ts'
import type { SystemInfo } from '../api/system.ts'

/**
 * 当前的系统信息；接口还没读到或读失败时是默认值。
 */
export const system = reactive({
  /**
   * 系统信息。
   */
  info: structuredClone(DEFAULT_SYSTEM_INFO) as SystemInfo,
})

/**
 * 换成新的系统信息，同时更新浏览器标签页的标题。
 * @param info 系统信息。
 */
export function applySystemInfo(info: SystemInfo): void {
  system.info = info
  document.title = info.systemName
}

/**
 * 从服务端读系统信息；读失败时保留默认值（不影响登录和使用）。
 * @returns 读完时结束。
 */
export async function loadSystemInfo(): Promise<void> {
  try {
    applySystemInfo(await getSystemInfo())
  } catch {
    // 服务端没启动或网络不通时，登录页本身会提示，这里不再重复报错。
  }
}

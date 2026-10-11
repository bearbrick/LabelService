/**
 * 服务端的字体：登录后读一次，上传、删除后更新；设计器的字体下拉框和“素材与字体”页共用。
 * 字体变了以后重新加载 /fonts/fonts.css，画布马上能用新字体。
 */
import { reactive } from 'vue'
import { listFonts } from '../api/resources.ts'
import type { FontList } from '../api/resources.ts'

/**
 * 服务端的字体；还没读到时为 null。
 */
export const fonts = reactive({
  /**
   * 字体列表。
   */
  list: null as FontList | null,
})

/**
 * 模板里可以写的字体名（服务端已有的）；还没读到时为空。
 * @returns 模板字体名。
 */
export function serverFamilies(): string[] {
  return fonts.list?.templateFamilies ?? []
}

/**
 * 读服务端的字体；读失败时保持原样（字体下拉框只显示内置的两种）。
 * @returns 读完时结束。
 */
export async function loadFonts(): Promise<void> {
  try {
    fonts.list = await listFonts()
  } catch {
    // 没有权限或网络不通时不影响设计器，下拉框退回内置的两种字体。
  }
}

/**
 * 换成上传、删除后的字体列表，并重新加载 fonts.css（地址加时间戳，避免用到缓存里的旧文件）。
 * @param list 新的字体列表。
 */
export function applyFonts(list: FontList): void {
  fonts.list = list
  const link = document.querySelector<HTMLLinkElement>('link[href^="/fonts/fonts.css"]')
  if (link) link.href = `/fonts/fonts.css?v=${Date.now()}`
}

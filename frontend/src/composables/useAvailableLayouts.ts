import { ref, watch } from 'vue'
import type { LayoutView } from '../model/layout.ts'

/**
 * 按当前模板和格子尺寸获取已启用的版式；切换条件或卸载后，过期查询不能更新状态。
 * @param codeOf 读取当前模板编码；未选择模板时不发起请求。
 * @param sizeOf 读取当前格子的宽高，单位为毫米。
 * @param load 根据格子尺寸查询版式，沿用调用方现有的权限和错误处理。
 * @returns 当前条件下的版式列表；等待新请求或当前请求失败时为空。
 */
export function useAvailableLayouts(codeOf: () => string | undefined, sizeOf: () => readonly [number, number], load: (width: number, height: number) => Promise<LayoutView[]>) {
  const layouts = ref<LayoutView[]>([])
  watch(
    () => [codeOf(), ...sizeOf()] as const,
    async ([code, width, height], _previous, onCleanup) => {
      let current = true
      // 清理发生在下一次查询及作用域停止时，成功和失败响应都受同一生命周期约束。
      onCleanup(() => { current = false })
      layouts.value = []
      if (!code) return
      try {
        const found = (await load(width, height)).filter((view) => view.enabled)
        if (current) layouts.value = found
      } catch {
        if (current) layouts.value = []
      }
    },
    { immediate: true },
  )
  return layouts
}

import { effectScope, nextTick, ref, watch } from 'vue'
import { useAvailableLayouts } from '../src/composables/useAvailableLayouts.ts'
import type { LayoutView } from '../src/model/layout.ts'

/**
 * 实际 Vue watcher，API Promise 的响应次序由测试控制；不使用 DOM、不接触任何真实服务。
 * @param check 记录每个竞态场景是否通过。
 * @returns 全部异步场景结束时完成。
 */
export async function checkAvailableLayouts(check: (name: string, value: boolean) => void): Promise<void> {
  const scope = effectScope()
  const code = ref<string | undefined>(undefined)
  const size = ref<readonly [number, number]>([100, 60])
  const pending: { width: number; height: number; resolve: (views: LayoutView[]) => void; reject: (reason: unknown) => void }[] = []
  const load = (width: number, height: number) => new Promise<LayoutView[]>((resolve, reject) => pending.push({ width, height, resolve, reject }))
  const layouts = scope.run(() => useAvailableLayouts(() => code.value, () => size.value, load))!
  let writes = 0
  const stopObserver = watch(layouts, () => { writes++ }, { flush: 'sync' })
  const flush = async () => { await nextTick(); await Promise.resolve(); await nextTick() }
  const item = (name: string, enabled = true): LayoutView => ({ enabled, updatedBy: 'fixture', updatedAt: '2026-10-10T00:00:00Z', layout: { code: name, name, paper: { width: 210, height: 297 }, slot: { width: 100, height: 60 }, slots: [], offset: { x: 0, y: 0 }, cutLines: false } })
  const visible = () => layouts.value.map((view) => view.layout.code).join(',')
  try {
    check('可用版式：没有模板时不请求', pending.length === 0 && !layouts.value.length)
    code.value = 'BOX_LABEL'; await flush()
    pending.at(-1)!.resolve([item('MATCH'), item('DISABLED', false)]); await flush()
    check('可用版式：当前请求可更新，禁用版式被过滤', visible() === 'MATCH' && pending[0].width === 100 && pending[0].height === 60)

    size.value = [70, 37]; await flush(); const oldSize = pending.at(-1)!
    size.value = [50, 30]; await flush(); const latestSize = pending.at(-1)!
    latestSize.resolve([item('NEW_SIZE')]); await flush()
    oldSize.resolve([item('OLD_SIZE')]); await flush()
    check('可用版式：同模板快速改变尺寸，旧成功响应不得覆盖新列表', visible() === 'NEW_SIZE')

    code.value = 'OTHER'; await flush(); const oldCode = pending.at(-1)!
    code.value = 'LATEST'; await flush(); const latestCode = pending.at(-1)!
    latestCode.resolve([item('LATEST_CODE')]); await flush()
    oldCode.reject(new Error('synthetic delayed older failure')); await flush()
    check('可用版式：旧请求失败不得清空新模板的列表', visible() === 'LATEST_CODE')

    size.value = [45, 25]; await flush(); const oldFailure = pending.at(-1)!
    size.value = [40, 20]; await flush(); const newSuccess = pending.at(-1)!
    newSuccess.resolve([item('LATEST_SIZE')]); await flush()
    oldFailure.reject(new Error('synthetic older size failure')); await flush()
    check('可用版式：同模板旧尺寸失败不得清空新尺寸的列表', visible() === 'LATEST_SIZE')

    code.value = 'BACK'; await flush(); const firstVisit = pending.at(-1)!
    code.value = 'AWAY'; await flush(); const away = pending.at(-1)!
    code.value = 'BACK'; await flush(); const secondVisit = pending.at(-1)!
    secondVisit.resolve([item('SECOND_VISIT')]); await flush()
    away.resolve([item('AWAY_RESULT')]); firstVisit.resolve([item('FIRST_VISIT')]); await flush()
    check('可用版式：切换模板再返回，旧模板及首次访问的响应都不能覆盖新请求', visible() === 'SECOND_VISIT')

    size.value = [35, 15]; await flush(); pending.at(-1)!.reject(new Error('synthetic current failure')); await flush()
    check('可用版式：当前请求失败时保持空列表', visible() === '')
    size.value = [30, 10]; await flush(); const cleared = pending.at(-1)!
    code.value = undefined; await flush(); cleared.resolve([item('CLEARED')]); await flush()
    check('可用版式：清空模板后，未完成请求不能重新填入列表', visible() === '')

    code.value = 'DISPOSE'; await flush(); const disposedSuccess = pending.at(-1)!
    size.value = [20, 5]; await flush(); const disposedFailure = pending.at(-1)!
    scope.stop(); const writesBefore = writes
    disposedSuccess.resolve([item('AFTER_DISPOSE')]); await flush()
    check('可用版式：卸载或停止作用域后，成功响应不得更新状态', visible() === '' && writes === writesBefore)
    disposedFailure.reject(new Error('synthetic failure after disposal')); await flush()
    check('可用版式：卸载或停止作用域后，失败响应也不能写入空列表', visible() === '' && writes === writesBefore)
  } finally {
    scope.stop()
    stopObserver()
  }
}

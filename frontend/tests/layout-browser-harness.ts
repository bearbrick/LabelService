/**
 * 浏览器回归专用桥接：直接装载真实导航组件和视口，不加入产品入口或生产包。
 */
import { createApp, h, nextTick, ref } from 'vue'
import NavToggle from '../src/components/admin/NavToggle.vue'
import { dialogs } from '../src/stores/dialogs.ts'
import { StageViewport } from '../src/editor/stageViewport.ts'
import { SheetStage } from '../src/editor/sheetStage.ts'
import sample from '../src/samples/box-label.json'

/**
 * 挂载真实导航组件，供回归控制页签状态并卸载。
 * @param host 专用 DOM 容器。
 * @returns 组件状态和生命周期操作。
 */
export function navigation(host: HTMLElement) {
  const auto = ref(true)
  dialogs.navCollapsed = false
  const app = createApp({ setup: () => () => h(NavToggle, { auto: auto.value }) })
  app.mount(host)
  return {
    collapsed: () => dialogs.navCollapsed,
    setAuto: async (value: boolean) => { auto.value = value; await nextTick() },
    dispose: () => { app.unmount(); dialogs.navCollapsed = false },
  }
}

/**
 * 创建两个真实 Konva 视口；只使用内存样例，不发送保存请求。
 * @param host 专用 DOM 容器。
 * @returns 缩放、窗口变化和清理操作。
 */
export function viewports(host: HTMLElement) {
  const template = structuredClone(sample)
  const layout = { code: 'ISOLATED_VIEW', name: '回归', paper: { width: 210, height: 297 }, slot: { width: 100, height: 60 }, slots: [], offset: { x: 0, y: 0 }, cutLines: false }
  const wraps = [0, 1].map(() => {
    const wrap = document.createElement('div')
    wrap.style.cssText = 'position:relative;width:800px;height:600px;overflow:hidden'
    const container = document.createElement('div')
    wrap.append(container); host.append(wrap)
    return { wrap, container }
  })
  const viewport = new StageViewport(wraps[0].wrap, wraps[0].container, {
    template: () => template, selection: () => [], select: () => {},
    settings: () => ({ showGrid: false, gridSize: 5, snap: false, panMode: false }),
    drawContext: () => ({}), contextSignature: () => '', committed: () => {},
    pointer: () => {}, viewChanged: () => {}, doubleClicked: () => {}, notify: () => {},
  } as any)
  const sheet = new SheetStage(wraps[1].wrap, wraps[1].container, {
    layout: () => layout, selection: () => [], select: () => {}, bad: () => new Set(), image: () => null,
    settings: () => ({ showGrid: false, grid: 5, snap: false }), committed: () => {},
  } as any)
  viewport.resize(); sheet.render()
  return {
    snapshot: () => [viewport, sheet].map(view => ({ zoom: view.zoom, position: view.stage.position(), size: view.stage.size() })),
    resize: (width: number, height: number) => {
      wraps.forEach(({ wrap }) => { wrap.style.width = `${width}px`; wrap.style.height = `${height}px` })
      viewport.resize(); sheet.resize()
    },
    fit: () => { viewport.zoomFit(); sheet.fit() },
    zoom: () => { viewport.zoomBy(1.25); sheet.zoomTo(sheet.zoom * 1.25) },
    pan: () => wraps.forEach(({ wrap }) => wrap.dispatchEvent(new WheelEvent('wheel', { deltaX: 20, deltaY: 15, bubbles: true, cancelable: true }))),
    requestFit: () => { template.page.width = 70; template.page.height = 40; viewport.requestFit() },
    dispose: () => { viewport.destroy(); sheet.destroy(); wraps.forEach(({ wrap }) => wrap.remove()) },
  }
}

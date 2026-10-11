/**
 * 设计器的状态：模板、选择、画布和视图、提示消息、绘制提示。撤销历史和自动保存在 designerHistory.ts，
 * 模板级操作在 designerTemplate.ts，元素操作在 designerElements.ts，字段操作在 designerFields.ts；
 * 画布、面板、快捷键统一从 designer.ts 导入，不直接改模板。
 * 每次修改后调用 commit() 记录撤销历史，并安排自动保存草稿：保存函数由模板工作区注册（PUT 草稿到服务端）。
 * 只读（没有编辑权限）时 commit() 会撤回刚才的修改。
 */
import { computed, reactive } from 'vue'
import type { Metric } from '../editor/draw.ts'
import type { DisplayMode } from '../model/bindings.ts'
import { normalizeElement } from '../model/elements.ts'
import type { LabelElement, LabelTemplate } from '../model/template.ts'
import { checkTemplate } from '../model/validate.ts'
import sample from '../samples/box-label.json'

/**
 * 画布提供给 store 的能力（由 CanvasWorkspace 注册）。
 */
export interface CanvasController {
  /**
   * 视图中心对应的纸面坐标。
   * @returns 纸面坐标，mm。
   */
  viewCenter(): { x: number; y: number }
  /**
   * 以画布中心为基点缩放。
   * @param factor 缩放倍数。
   */
  zoomBy(factor: number): void
  /**
   * 缩放到指定比例。
   * @param zoom 缩放比例，1 表示实际大小。
   */
  zoomTo(zoom: number): void
  /**
   * 整张纸放进可见区域。
   */
  zoomFit(): void
  /**
   * 下次尺寸可用时适应窗口（换模板时用）。
   */
  requestFit(): void
  /**
   * 开始直接编辑文本元素。
   * @param id 元素 ID。
   */
  editText(id: string): void
  /**
   * 把纸张导出成指定像素尺寸的白底图片（和服务端预览对比用）。
   * @param width 宽度，像素。
   * @param height 高度，像素。
   * @returns 白底画布；画布还没创建时为 null。
   */
  snapshot(width: number, height: number): HTMLCanvasElement | null
}

/**
 * 一条提示消息（右下角弹出，几秒后自动消失）。
 */
export interface Toast {
  /**
   * 消息 ID（自增），删除时按它找。
   */
  id: number
  /**
   * 提示文字。
   */
  text: string
  /**
   * 是不是错误（红色，显示更久）。
   */
  error: boolean
}

/**
 * 保存草稿的函数（由模板工作区注册），失败时抛错。
 */
export type DraftSaver = (template: LabelTemplate) => Promise<void>

/**
 * 当前注册的画布；设计器没打开时为 null。
 */
let canvas: CanvasController | null = null

/**
 * 提示消息的自增 ID。
 */
let toastId = 0

/**
 * 设计器的状态（响应式），界面直接读；修改模板请用操作函数。
 */
export const designer = reactive({
  /**
   * 正在编辑的模板（默认是示例箱标）。
   */
  template: copyOf(sample as unknown as LabelTemplate),
  /**
   * 选中的元素 ID。
   */
  selection: [] as string[],
  /**
   * 绘制时回报的提示（元素 ID → 提示），属性面板显示。
   */
  metrics: {} as Record<string, Metric>,
  /**
   * 能不能撤销。
   */
  canUndo: false,
  /**
   * 能不能重做。
   */
  canRedo: false,
  /**
   * 有没有还没保存到服务端的修改。
   */
  dirty: false,
  /**
   * 正在保存草稿。
   */
  saving: false,
  /**
   * 上次保存失败的原因；成功后清空。
   */
  saveError: '',
  /**
   * 上次保存成功的时间（HH:mm:ss）。
   */
  savedAt: '',
  /**
   * 只读（没有编辑权限）：修改会被撤回。
   */
  readOnly: false,
  /**
   * 图片等异步资源加载完成时加一，让画布重画。
   */
  renderVersion: 0,
  /**
   * 正在显示的提示消息。
   */
  toasts: [] as Toast[],
  /**
   * 界面状态（不属于模板，不进撤销历史）。
   */
  ui: {
    /**
     * 绑定显示样例值还是占位符。
     */
    mode: 'sample' as DisplayMode,
    /**
     * 显示网格。
     */
    showGrid: true,
    /**
     * 网格间距，mm。
     */
    gridSize: 1,
    /**
     * 拖动时吸附参考线和网格。
     */
    snap: true,
    /**
     * 当前缩放比例（画布回报，状态栏显示）。
     */
    zoom: 1,
    /**
     * 光标在纸面上的 x，mm；不在画布上时为 null。
     */
    cursorX: null as number | null,
    /**
     * 光标在纸面上的 y，mm；不在画布上时为 null。
     */
    cursorY: null as number | null,
    /**
     * 按住了空格（拖动平移画布）。
     */
    spaceDown: false,
    /**
     * 左侧面板当前的页签：元素、字段、图层。
     */
    leftTab: 'elements' as 'elements' | 'fields' | 'layers',
    /**
     * 画布上方的操作提示（如拖放时“松开放置元素”）。
     */
    tip: '',
    /**
     * 正有东西拖到画布上（画布显示高亮）。
     */
    dropActive: false,
    /**
     * 快捷键说明对话框是否打开。
     */
    shortcutsOpen: false,
  },
})

/**
 * 选中的元素（按选择顺序，去掉已不存在的）。
 */
export const selectedElements = computed(() =>
  designer.selection.map((id) => designer.template.elements.find((element) => element.id === id)).filter((element): element is LabelElement => !!element),
)

/**
 * 只选中一个元素时是它，否则为 null（属性面板用）。
 */
export const selectedElement = computed(() => (selectedElements.value.length === 1 ? selectedElements.value[0] : null))

/**
 * 选中的元素是否都已锁定（工具栏的锁定按钮显示状态）。
 */
export const allSelectedLocked = computed(() => selectedElements.value.length > 0 && selectedElements.value.every((element) => element.locked))

/**
 * 模板的静态检查结果（未定义的绑定、没用到的字段、不能导出的问题）。
 */
export const report = computed(() => checkTemplate(designer.template))

/**
 * 注册画布（CanvasWorkspace 挂载时注册，卸载时传 null）。
 * @param controller 画布；null 表示注销。
 */
export function registerCanvas(controller: CanvasController | null): void {
  canvas = controller
}

/**
 * 弹出一条提示消息，普通的 2.6 秒、错误的 4.2 秒后消失。
 * @param text 提示文字。
 * @param error 是不是错误。
 */
export function toast(text: string, error = false): void {
  const id = ++toastId
  designer.toasts.push({ id, text, error })
  setTimeout(() => {
    const index = designer.toasts.findIndex((item) => item.id === id)
    if (index >= 0) designer.toasts.splice(index, 1)
  }, error ? 4200 : 2600)
}

/**
 * 深复制模板并补全默认值（字段、元素的默认属性、DPI 300）。
 * @param template 模板。
 * @returns 新模板。
 */
export function copyOf(template: LabelTemplate): LabelTemplate {
  const copy = JSON.parse(JSON.stringify(template)) as LabelTemplate
  copy.fields ??= []
  copy.elements = (copy.elements ?? []).map((element) => normalizeElement(element))
  copy.page.dpi ??= 300
  return copy
}

/* ---------------- 画布快照 ---------------- */

/**
 * 当前画布的纸张快照；设计器没打开时返回 null。
 * @param width 宽度，像素。
 * @param height 高度，像素。
 * @returns 白底画布，或 null。
 */
export function canvasSnapshot(width: number, height: number): HTMLCanvasElement | null {
  return canvas?.snapshot(width, height) ?? null
}

/* ---------------- 选择 ---------------- */

/**
 * 按 ID 找模板里的元素。
 * @param id 元素 ID。
 * @returns 元素；不存在时为 undefined。
 */
export function findElement(id: string): LabelElement | undefined {
  return designer.template.elements.find((element) => element.id === id)
}

/**
 * 改变选择。
 * @param ids 新选中的元素 ID。
 */
export function select(ids: string[]): void {
  designer.selection = ids
}

/**
 * 选中全部可见元素。
 */
export function selectAll(): void {
  designer.selection = designer.template.elements.filter((element) => !element.hidden).map((element) => element.id)
}

/* ---------------- 画布与视图 ---------------- */

/**
 * 视图中心对应的纸面坐标（新元素放在这里）。
 * @returns 纸面坐标，mm；画布还没创建时为 null。
 */
export function viewCenter(): { x: number; y: number } | null {
  return canvas?.viewCenter() ?? null
}

/**
 * 下次画布尺寸可用时适应窗口（打开、导入模板时用）。
 */
export function requestFit(): void {
  canvas?.requestFit()
}

/**
 * 开始直接编辑文本元素（画布上弹出输入框）。
 * @param id 元素 ID。
 */
export function editText(id: string): void {
  canvas?.editText(id)
}

/**
 * 放大一档（×1.25）。
 */
export function zoomIn(): void {
  canvas?.zoomBy(1.25)
}

/**
 * 缩小一档（×0.8）。
 */
export function zoomOut(): void {
  canvas?.zoomBy(0.8)
}

/**
 * 实际大小（100%）。
 */
export function zoomActual(): void {
  canvas?.zoomTo(1)
}

/**
 * 适应窗口。
 */
export function zoomFit(): void {
  canvas?.zoomFit()
}

/* ---------------- 绘制提示 ---------------- */

/**
 * 绘制时回报的提示，内容没变时不写，避免多余的界面刷新。
 * @param id 元素 ID。
 * @param metric 提示。
 */
export function setMetric(id: string, metric: Metric): void {
  const current = designer.metrics[id]
  if (current && current.text === metric.text && current.tone === metric.tone) return
  designer.metrics[id] = metric
}

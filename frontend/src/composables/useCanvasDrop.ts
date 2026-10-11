/**
 * 从左侧面板拖到画布：元素、图标、素材放到落点；字段拖到条码或图片上绑定，拖到空白处生成“名称：{{key}}”文本；
 * 从电脑拖入图片文件时先上传成素材。拖动过程中在画布上方提示松开后会发生什么。由 CanvasWorkspace 使用。
 */
import type { Ref } from 'vue'
import { elementBounds } from '../editor/geometry.ts'
import type { DesignerStage } from '../editor/stage.ts'
import { defaultTextStyle } from '../model/elements.ts'
import type { FieldDefinition, LabelElement } from '../model/template.ts'
import { uploadAsset } from '../stores/assets.ts'
import { addElement, commit, designer, findElement, select, toast } from '../stores/designer.ts'
import { drag, endDrag } from '../stores/drag.ts'

/**
 * 字段能不能绑定到元素上：条码、二维码接受非图片字段，图片接受图片字段；锁定的元素不行。
 * @param element 落点下的元素。
 * @param field 拖动的字段。
 * @returns 能绑定时为 true。
 */
function bindable(element: LabelElement | undefined, field: FieldDefinition | undefined): element is LabelElement {
  if (!element || !field || element.locked) return false
  if (element.type === 'barcode' || element.type === 'qrcode') return field.type !== 'image'
  return element.type === 'image' && field.type === 'image'
}

/**
 * 放下字段：能绑定就绑定到目标元素；图片字段生成图片元素；其他生成自动缩小的文本元素。
 * @param key 字段 key。
 * @param target 落点下的元素。
 * @param point 落点，mm。
 */
function dropField(key: string, target: LabelElement | undefined, point?: { x: number; y: number }): void {
  const field = designer.template.fields.find((item) => item.key === key)
  if (!field) return
  if (bindable(target, field)) {
    if (target.type === 'image') target.src = `{{${key}}}`
    else if (target.type === 'barcode' || target.type === 'qrcode') target.value = `{{${key}}}`
    select([target.id])
    commit()
    toast(`已把 ${key} 绑定到“${target.name}”`)
  } else if (field.type === 'image') {
    addElement('image', { src: `{{${key}}}`, name: field.name || key, width: 20, height: 20 }, point)
  } else {
    const label = field.name || key
    addElement('text', { content: `${label}：{{${key}}}`, name: label, width: 40, height: 5, style: { ...defaultTextStyle(), fontSize: 9, fit: 'shrink' } }, point)
  }
}

/**
 * 画布的拖放处理。
 * @param getStage 取当前的画布（还没创建时为 null）。
 * @param wrap 画布的外层容器（判断拖动是不是离开了画布）。
 * @returns dragover、dragleave、drop 的处理函数，以及清除拖放提示的函数。
 */
export function useCanvasDrop(getStage: () => DesignerStage | null, wrap: Ref<HTMLDivElement | undefined>) {
  /**
   * 拖动经过画布：允许放下，并按拖动的内容提示；字段经过能绑定的元素时高亮它。
   * @param event 拖动事件。
   */
  function onDragOver(event: DragEvent): void {
    const stage = getStage()
    const payload = drag.payload
    const files = !!event.dataTransfer?.types.includes('Files')
    if (!stage || (!payload && !files)) return
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy'
    designer.ui.dropActive = true
    stage.highlight(null)
    if (payload?.kind === 'field') {
      const field = designer.template.fields.find((item) => item.key === payload.key)
      const id = stage.elementAt(event)
      const target = id ? findElement(id) : undefined
      if (bindable(target, field)) {
        stage.highlight(elementBounds(target))
        designer.ui.tip = `松开把 ${payload.key} 绑定到“${target.name}”`
      } else {
        designer.ui.tip = `松开生成“${field?.name || payload.key}：{{${payload.key}}}”文本`
      }
    } else {
      designer.ui.tip = files ? '松开上传图片并放到这里' : '松开放置元素'
    }
  }

  /**
   * 清除拖放的高亮和提示。
   */
  function clearDrop(): void {
    designer.ui.dropActive = false
    designer.ui.tip = ''
    getStage()?.highlight(null)
  }

  /**
   * 拖动离开画布（进入画布里的子元素不算）。
   * @param event 拖动事件。
   */
  function onDragLeave(event: DragEvent): void {
    if (!wrap.value?.contains(event.relatedTarget as Node | null)) clearDrop()
  }

  /**
   * 放下：图片文件先上传再生成图片元素；元素、图标、素材生成新元素；字段按落点下的元素绑定或生成文本。
   * @param event 拖放事件。
   * @returns 处理完成时结束（上传失败时提示，不抛）。
   */
  async function onDrop(event: DragEvent): Promise<void> {
    event.preventDefault()
    const stage = getStage()
    if (!stage) return
    const point = stage.pointerMm(event) ?? undefined
    const payload = drag.payload
    const target = payload?.kind === 'field' ? stage.elementAt(event) : null
    endDrag()
    clearDrop()
    const file = event.dataTransfer?.files?.[0]
    if (!payload && file) {
      try {
        const asset = await uploadAsset(file)
        addElement('image', { src: `asset:${asset.key}`, name: asset.name }, point)
      } catch (error) {
        toast((error as Error).message, true)
      }

      return
    }

    if (!payload) return
    if (payload.kind === 'element') addElement(payload.type, {}, point)
    else if (payload.kind === 'icon') addElement('icon', { icon: payload.icon, name: payload.name }, point)
    else if (payload.kind === 'asset') addElement('image', { src: `asset:${payload.id}`, name: payload.name }, point)
    else dropField(payload.key, target ? findElement(target) : undefined, point)
  }

  return { onDragOver, onDragLeave, onDrop, clearDrop }
}

/**
 * 新建元素、补全默认值、复制元素。新建的元素把默认值写全，属性面板可以直接绑定。
 */
import { DEFAULT_ICON } from './icons.ts'
import { ELEMENT_LABELS, STROKE_DEFAULT_WIDTH, TEXT_DEFAULTS } from './template.ts'
import type { ElementType, FieldDefinition, LabelElement, TextStyle } from './template.ts'
import { roundMm } from './units.ts'

export interface ElementMeta {
  /** 中文名。 */
  label: string
  /** 界面图标名（见 components/icons.ts）。 */
  icon: string
  /** 新建时的默认尺寸（mm）。 */
  width: number
  height: number
}

export const ELEMENT_META: Readonly<Record<ElementType, ElementMeta>> = {
  text: { label: ELEMENT_LABELS.text, icon: 'text', width: 32, height: 6 },
  barcode: { label: ELEMENT_LABELS.barcode, icon: 'barcode', width: 50, height: 15 },
  qrcode: { label: ELEMENT_LABELS.qrcode, icon: 'qrcode', width: 18, height: 18 },
  image: { label: ELEMENT_LABELS.image, icon: 'image', width: 24, height: 8 },
  icon: { label: ELEMENT_LABELS.icon, icon: 'shapes', width: 8, height: 8 },
  line: { label: ELEMENT_LABELS.line, icon: 'line', width: 40, height: 0 },
  rect: { label: ELEMENT_LABELS.rect, icon: 'rect', width: 30, height: 20 },
}

/** 默认虚线：实段 1mm、空段 0.6mm。 */
export const DEFAULT_DASH = [1, 0.6]

/** 写全的文字样式，和服务端 TextStyle 的默认值一致。 */
export function defaultTextStyle(): Required<Omit<TextStyle, 'minFontSize' | 'letterSpacing'>> & Pick<TextStyle, 'minFontSize' | 'letterSpacing'> {
  return {
    fontFamily: TEXT_DEFAULTS.fontFamily,
    fontSize: TEXT_DEFAULTS.fontSize,
    bold: false,
    italic: false,
    underline: false,
    color: TEXT_DEFAULTS.color,
    align: TEXT_DEFAULTS.align,
    vAlign: TEXT_DEFAULTS.vAlign,
    fit: TEXT_DEFAULTS.fit,
    minFontSize: 6,
    lineHeight: TEXT_DEFAULTS.lineHeight,
    letterSpacing: 0,
  }
}

/** 生成一个不和已有元素重复的 ID。 */
export function newElementId(existing: readonly { id: string }[] = []): string {
  for (;;) {
    const id = 'e' + Math.random().toString(36).slice(2, 9)
    if (!existing.some((element) => element.id === id)) return id
  }
}

/** “文本 3” 这样的默认名字。 */
export function defaultName(type: ElementType, existing: readonly LabelElement[]): string {
  const count = existing.filter((element) => element.type === type).length + 1
  return `${ELEMENT_META[type].label} ${count}`
}

/** 补全元素的默认值（原地修改），返回同一个对象。 */
export function normalizeElement(element: LabelElement): LabelElement {
  element.name ??= ELEMENT_META[element.type].label
  element.rotation ??= 0
  element.locked ??= false
  element.hidden ??= false
  switch (element.type) {
    case 'text':
      element.content ??= ''
      element.style = { ...defaultTextStyle(), ...element.style }
      break
    case 'barcode':
      element.symbology ??= 'code128'
      element.value ??= ''
      element.align ??= 'left'
      element.showText ??= true
      element.textPosition ??= 'bottom'
      element.minModuleDots ??= 2
      element.textStyle = { fontSize: 8, ...element.textStyle }
      break
    case 'qrcode':
      element.symbology ??= 'qr'
      element.value ??= ''
      element.ecLevel ??= 'M'
      break
    case 'image':
      element.src ??= ''
      element.fit ??= 'contain'
      element.mono ??= false
      break
    case 'icon':
      element.icon ??= DEFAULT_ICON
      element.color ??= '#000000'
      break
    case 'line':
      // 线不设 stroke 时按默认线宽画（和服务端 LinePainter 一致）。
      element.stroke = { width: STROKE_DEFAULT_WIDTH, color: '#000000', ...element.stroke }
      break
    case 'rect':
      // 矩形不设 stroke 表示没有边框，补全时线宽记为 0，保持原意。
      element.stroke = { width: element.stroke ? STROKE_DEFAULT_WIDTH : 0, color: '#000000', ...element.stroke }
      element.radius ??= 0
      break
  }

  return element
}

/** 新建一个元素。条码默认绑定第一个非图片字段，图片默认绑定第一个图片字段。 */
export function makeElement(
  type: ElementType,
  fields: readonly FieldDefinition[],
  existing: readonly LabelElement[],
  extra: Partial<LabelElement> = {},
): LabelElement {
  const meta = ELEMENT_META[type]
  const firstValueField = fields.find((field) => field.type !== 'image')
  const firstImageField = fields.find((field) => field.type === 'image')
  // id、type 放在最前面，导出的 JSON 好读。
  const id = newElementId(existing)
  const base = { x: 0, y: 0, width: meta.width, height: meta.height, name: defaultName(type, existing) }
  let element: LabelElement
  switch (type) {
    case 'text':
      element = { id, type, ...base, content: '双击编辑文本' }
      break
    case 'barcode':
      element = { id, type, ...base, value: firstValueField ? `{{${firstValueField.key}}}` : '123456789012' }
      break
    case 'qrcode':
      element = { id, type, ...base, value: firstValueField ? `{{${firstValueField.key}}}` : 'https://example.com' }
      break
    case 'image':
      element = { id, type, ...base, src: firstImageField ? `{{${firstImageField.key}}}` : 'asset:logo' }
      break
    case 'icon':
      element = { id, type, ...base, icon: DEFAULT_ICON }
      break
    case 'line':
      element = { id, type, ...base }
      break
    case 'rect':
      element = { id, type, ...base, stroke: { width: STROKE_DEFAULT_WIDTH, color: '#000000' } }
      break
    default:
      throw new Error(`未知的元素类型：${type as string}`)
  }

  return normalizeElement(Object.assign(element, extra))
}

/** 深复制元素，换新 ID、偏移位置、解除锁定。 */
export function cloneElements(elements: readonly LabelElement[], offset: number, existing: readonly LabelElement[]): LabelElement[] {
  const taken = [...existing]
  return elements.map((element) => {
    const copy = normalizeElement(JSON.parse(JSON.stringify(element)) as LabelElement)
    copy.id = newElementId(taken)
    copy.x = roundMm(copy.x + offset)
    copy.y = roundMm(copy.y + offset)
    copy.locked = false
    taken.push(copy)
    return copy
  })
}

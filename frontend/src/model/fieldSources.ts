/**
 * 字段来源与互斥配置的纯逻辑，旧字段保持兼容规则，显式操作后才改变来源。
 */
import { AUTO_PRESETS, autoPreview, buildAuto, parseAuto } from './autoValues.ts'
import type { AutoPreset } from './autoValues.ts'
import type { FieldDefinition } from './template.ts'

/**
 * 字段在来源选择框中的状态，legacy 只用于有旧回退配置的字段。
 */
export type FieldSourceView = 'input' | 'template' | 'legacy'

/**
 * 是否保存了手写值，数字零和布尔假也算有效值。
 * @param field 字段。
 * @returns 是否有值。
 */
export function hasLiteral(field: FieldDefinition): boolean {
  return field.default !== undefined && field.default !== null && field.default !== ''
}

/**
 * 读取来源，不在打开旧模板时隐式迁移。
 * @param field 字段。
 * @returns 来源选项。
 */
export function fieldSource(field: FieldDefinition): FieldSourceView {
  return field.source ?? (field.auto || hasLiteral(field) ? 'legacy' : 'input')
}

/**
 * 画布按来源取预览内容，模板提供不接受样例覆盖，接口来源只使用业务样例。
 * @param field 字段。
 * @returns 预览值。
 */
export function previewFieldValue(field: FieldDefinition): unknown {
  if (field.source === 'template') return field.auto ? autoPreview(field) : field.default
  if (field.source === 'input') return field.sample
  return (field.auto ? autoPreview(field) : undefined) ?? field.sample ?? field.default
}

/**
 * 选择明确来源时清理冲突配置，模板提供的必填不适用。
 * @param field 字段。
 * @param source 新来源。
 */
export function changeSource(field: FieldDefinition, source: 'input' | 'template'): void {
  const previous = fieldSource(field)
  field.source = source
  if (source === 'input') {
    delete field.auto
    delete field.default
    if (previous !== 'input') field.required = true
  } else {
    field.required = false
    if (field.auto) delete field.default
  }
}

/**
 * 常用系统预设只提供日期和时间；旧模板正在使用的其他预设保留显示。
 * @param field 字段。
 * @returns 可选择的预设。
 */
export function presetOptions(field: FieldDefinition): AutoPreset[] {
  const options = field.type === 'image' ? [] : AUTO_PRESETS.date.slice(0, 2)
  const kind = parseAuto(field.auto ?? '')?.kind
  const previous = AUTO_PRESETS[field.type].find((preset) => preset.kind === kind)
  if (previous && !options.some((preset) => preset.kind === kind)) options.push(previous)
  return options
}

/**
 * 选择系统预设，自动采用日期类型，清空手写值；选择无时恢复手写。
 * @param field 字段。
 * @param kind 预设种类，空字符串表示无。
 * @param n 旧相对日期预设的参数。
 */
export function changePreset(field: FieldDefinition, kind: string, n?: number): void {
  changeSource(field, 'template')
  if (!kind) { delete field.auto; return }
  if (kind === 'today' || kind === 'now') {
    if (field.type !== 'date') delete field.format
    field.type = 'date'
  }
  field.auto = buildAuto(kind, n ?? parseAuto(field.auto ?? '')?.n)
  delete field.default
}

/**
 * 保存手写值，不把 now 等文字当成表达式；数值字段保留数字零。
 * @param field 字段。
 * @param value 用户输入。
 */
export function changeLiteral(field: FieldDefinition, value: string): void {
  changeSource(field, 'template')
  delete field.auto
  if (value === '') delete field.default
  else field.default = field.type === 'number' && Number.isFinite(Number(value)) ? Number(value) : value
}

/**
 * 调用样例：按字段定义生成开放接口的请求体，数据源页和接口调试用。
 * 取值规则和后端发布检查的样例数据一致：有自动值的字段不传（照着样例传会盖掉自动值）；有样例值用样例值；非必填或有默认值的字段不传；其余按类型给占位值。
 */
import type { FieldDefinition, LabelTemplate } from './template.ts'

/**
 * 1×1 白色 PNG，图片字段没有样例时的占位值。
 */
export const PLACEHOLDER_IMAGE = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4//8/AwAI/AL+XJ/PAAAAAElFTkSuQmCC'

/**
 * 一个字段在调用样例里的值；不需要传时返回 undefined。
 * @param field 字段定义。
 * @returns 样例值、按类型的占位值，或 undefined（不传）。
 */
export function sampleValue(field: FieldDefinition): unknown {
  if (field.auto) return undefined
  if (field.sample !== undefined && field.sample !== null && field.sample !== '') return field.sample
  const hasDefault = field.default !== undefined && field.default !== null && field.default !== ''
  if (field.required === false || hasDefault) return undefined
  switch (field.type) {
    case 'number':
      return 0
    case 'date':
      return '2026-01-01'
    case 'image':
      return PLACEHOLDER_IMAGE
    default:
      return field.key
  }
}

/**
 * 调用样例里的 data。
 * @param fields 字段定义。
 * @returns 字段 key → 值（不需要传的字段不在里面）。
 */
export function sampleData(fields: readonly FieldDefinition[]): Record<string, unknown> {
  const data: Record<string, unknown> = {}
  for (const field of fields) {
    const value = sampleValue(field)
    if (value !== undefined) data[field.key] = value
  }
  return data
}

/**
 * 单张生成（POST /api/v1/render）的请求体。
 * @param template 模板（只用编码和字段）。
 * @param format 输出格式，默认 pdf。
 * @returns 请求体。
 */
export function sampleRequest(template: Pick<LabelTemplate, 'code' | 'fields'>, format = 'pdf'): Record<string, unknown> {
  return { templateCode: template.code, format, data: sampleData(template.fields) }
}

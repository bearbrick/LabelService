/**
 * 模板的静态检查：字段 key 格式和唯一性、元素 ID 唯一、绑定是否引用了未定义的字段、哪些字段没被引用。
 * 规则和后端检查程序（ContractChecks）一致，导出 JSON 前也会检查。
 */
import { usedKeys } from './bindings.ts'
import type { LabelTemplate } from './template.ts'

export interface TemplateReport {
  /** 绑定里引用了、但字段列表里没有的 key。 */
  undefinedKeys: string[]
  /** 定义了、但没有元素引用的字段。 */
  unusedFields: string[]
  /** 不能导出的问题。 */
  errors: string[]
}

export const FIELD_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/

export function checkTemplate(template: LabelTemplate): TemplateReport {
  const errors: string[] = []
  const keys = template.fields.map((field) => field.key)
  for (const key of keys) {
    if (!FIELD_KEY_PATTERN.test(key)) errors.push(`字段 key“${key}”不合法：要以字母开头，只含字母、数字、下划线`)
  }

  const duplicateKeys = keys.filter((key, index) => keys.indexOf(key) !== index)
  for (const key of new Set(duplicateKeys)) errors.push(`字段 key“${key}”重复`)
  const ids = template.elements.map((element) => element.id)
  const duplicateIds = ids.filter((id, index) => ids.indexOf(id) !== index)
  for (const id of new Set(duplicateIds)) errors.push(`元素 ID“${id}”重复`)
  if (!(template.page.width > 0) || !(template.page.height > 0)) errors.push('画布宽高必须大于 0')

  const used = usedKeys(template.elements)
  const undefinedKeys = [...used].filter((key) => !keys.includes(key))
  for (const key of undefinedKeys) errors.push(`绑定引用了未定义的字段 ${key}`)
  const unusedFields = keys.filter((key) => !used.has(key))
  return { undefinedKeys, unusedFields, errors }
}

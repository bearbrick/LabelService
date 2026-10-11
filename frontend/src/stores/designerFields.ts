/**
 * 设计器的字段操作：添加、改 key（引用它的元素一起改）、删除、把字段插入选中的元素。
 */
import { renameKey } from '../model/bindings.ts'
import type { FieldDefinition, FieldType } from '../model/template.ts'
import { FIELD_KEY_PATTERN } from '../model/validate.ts'
import { commit } from './designerHistory.ts'
import { designer, selectedElement, toast } from './designerState.ts'

/**
 * 添加字段；key 不合法或重复时返回错误说明。
 * @param key 字段 key。
 * @param type 字段类型。
 * @param name 中文名称。
 * @returns 错误说明；成功时为 null。
 */
export function addField(key: string, type: FieldType, name?: string): string | null {
  const trimmed = key.trim()
  if (!FIELD_KEY_PATTERN.test(trimmed)) return 'key 要以字母开头，只含字母、数字、下划线'
  if (designer.template.fields.some((field) => field.key === trimmed)) return `字段 ${trimmed} 已存在`
  designer.template.fields.push({ key: trimmed, name: name || undefined, type, required: true })
  commit()
  return null
}

/**
 * 改字段 key，模板里引用它的元素一起改；新 key 不合法或重复时返回错误说明。
 * @param field 字段。
 * @param key 新 key。
 * @returns 错误说明；成功或没变时为 null。
 */
export function renameField(field: FieldDefinition, key: string): string | null {
  const trimmed = key.trim()
  if (trimmed === field.key) return null
  if (!FIELD_KEY_PATTERN.test(trimmed)) return 'key 要以字母开头，只含字母、数字、下划线'
  if (designer.template.fields.some((item) => item !== field && item.key === trimmed)) return `字段 ${trimmed} 已存在`
  const from = field.key
  field.key = trimmed
  for (const element of designer.template.elements) {
    if (element.type === 'text') element.content = renameKey(element.content, from, trimmed)
    else if (element.type === 'barcode' || element.type === 'qrcode') element.value = renameKey(element.value, from, trimmed)
    else if (element.type === 'image') element.src = renameKey(element.src, from, trimmed)
  }

  commit()
  return null
}

/**
 * 删除字段（引用它的绑定保留，模板检查会提示未定义的字段）。
 * @param field 字段。
 */
export function removeField(field: FieldDefinition): void {
  designer.template.fields = designer.template.fields.filter((item) => item !== field)
  commit()
}

/**
 * 把字段插入选中的文本，或绑定到选中的条码。
 * @param key 字段 key。
 */
export function insertFieldIntoSelection(key: string): void {
  const element = selectedElement.value
  if (element?.type === 'text') {
    element.content += `{{${key}}}`
    commit()
    toast(`已插入 {{${key}}}`)
  } else if (element?.type === 'barcode' || element?.type === 'qrcode') {
    element.value = `{{${key}}}`
    commit()
    toast(`已绑定 {{${key}}}`)
  } else {
    toast('先选中一个文本或条码元素', true)
  }
}

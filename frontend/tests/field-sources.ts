/**
 * 字段来源、互斥配置、兼容及样例/画布取值检查。
 */
import { changeLiteral, changePreset, changeSource, fieldSource, presetOptions, previewFieldValue } from '../src/model/fieldSources.ts'
import { resolveText } from '../src/model/bindings.ts'
import { sampleData } from '../src/model/request.ts'
import { checkTemplate } from '../src/model/validate.ts'
import type { FieldDefinition, LabelTemplate } from '../src/model/template.ts'

/**
 * 检查字段配置完整的取值行为。
 * @param check 记录结果。
 */
export function checkFieldSources(check: (name: string, value: boolean) => void): void {
  const field: FieldDefinition = { key: 'qty', type: 'number', default: 0, sample: 99, required: true }
  check('旧回退字段识别为兼容模式，打开不修改来源', fieldSource(field) === 'legacy' && field.source === undefined)
  changeSource(field, 'template')
  check('模板提供取消接口必填且保留数字零', field.required === false && field.default === 0)
  check('模板提供从样例排除，画布取真实手写值而非 sample', !('qty' in sampleData([field]))
    && resolveText('{{qty}}', [field], 'sample') === '0')
  changePreset(field, 'now')
  check('系统预设采用日期类型，清空手写值', field.auto === 'now' && field.type === 'date' && field.default === undefined)
  check('系统预设仅展开常用日期时间两项', presetOptions(field).length === 2)
  changePreset(field, '')
  check('系统预设选无后可以重新手写', field.auto === undefined)
  field.type = 'string'
  changeLiteral(field, 'now')
  check('手写 now 原样打印，不解析为系统预设', field.auto === undefined && field.default === 'now'
    && resolveText('{{qty}}', [field], 'sample') === 'now')
  changeSource(field, 'input')
  check('接口来源清空模板配置并恢复必填', field.source === 'input' && field.default === undefined && field.auto === undefined
    && field.required === true && sampleData([field]).qty === 99)
  changeSource(field, 'template')
  check('模板空值允许留空，画布不显示样例或字段占位符', resolveText('X{{qty}}Y', [field], 'sample') === 'XY')
  const legacy: FieldDefinition = { key: 'expiry', type: 'date', auto: 'today+30d' }
  check('旧相对日期预设保留显示，未迁移配置', presetOptions(legacy).some(preset => preset.kind === 'today+Nd') && !legacy.source)
  check('模板图片预览使用手写内容，不被样例图片覆盖', previewFieldValue({ key: 'logo', type: 'image', source: 'template', default: 'fixed-image', sample: 'old-image' }) === 'fixed-image')
  const invalid = { code: 'CHECK', name: '检查', schemaVersion: '1.0', page: { width: 100, height: 60 }, elements: [],
    fields: [{ key: 'bad', type: 'date', source: 'template', auto: 'now', default: '2026-01-01' }] } as LabelTemplate
  check('导出前拦截互斥配置', checkTemplate(invalid).errors.some(error => error.includes('不能同时')))
  invalid.fields = [{ key: 'bad', type: 'string', source: 'input', default: '固定' }]
  check('导出前拦截接口字段的手写值', checkTemplate(invalid).errors.some(error => error.includes('接口来源')))
}

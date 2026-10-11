/**
 * 设计器里显示绑定：把 `{{key}}`、`{{key:format}}` 换成字段的样例值，或者保留占位符。
 * 规则和后端 BindingResolver 一致：绑定里的格式优先，其次字段的 format，再次默认格式。
 * 这里只支持常用的 .NET 格式写法，用于预览；最终效果以服务端预览为准。
 */
import type { FieldDefinition, FieldType, LabelElement } from './template.ts'

/**
 * 显示方式：sample 用样例值，placeholder 原样显示 `{{key}}`。
 */
export type DisplayMode = 'sample' | 'placeholder'

/**
 * 绑定的写法 {{key}} 或 {{key:format}}：第 1 组是字段 key，第 2 组是格式（可以没有）。
 */
const BINDING = /\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*(?::([^}]*))?\}\}/g

/**
 * 文本里引用的全部字段 key，按出现顺序去重。
 * @param text 文本；可以为 undefined。
 * @returns 字段 key 列表；没有绑定时为空。
 */
export function keysIn(text: string | undefined): string[] {
  if (!text || !text.includes('{{')) return []
  const keys: string[] = []
  for (const match of text.matchAll(BINDING)) {
    if (!keys.includes(match[1])) keys.push(match[1])
  }
  return keys
}

/**
 * 把文本里对字段 from 的绑定改成 to，格式部分不变（改字段 key 时用）。
 * @param text 文本。
 * @param from 原来的字段 key。
 * @param to 新的字段 key。
 * @returns 改好的文本。
 */
export function renameKey(text: string, from: string, to: string): string {
  if (!text || !text.includes('{{')) return text
  return text.replace(BINDING, (whole, key: string) => (key === from ? whole.replace(key, to) : whole))
}

/**
 * 元素里可以写绑定的那个属性的内容。
 * @param element 元素。
 * @returns 文本内容、条码值或图片来源；其他元素为空字符串。
 */
export function bindingTextOf(element: LabelElement): string {
  switch (element.type) {
    case 'text':
      return element.content
    case 'barcode':
    case 'qrcode':
      return element.value
    case 'image':
      return element.src
    default:
      return ''
  }
}

/**
 * 模板全部元素引用到的字段 key。
 * @param elements 元素。
 * @returns 被引用的字段 key。
 */
export function usedKeys(elements: readonly LabelElement[]): Set<string> {
  const keys = new Set<string>()
  for (const element of elements) {
    for (const key of keysIn(bindingTextOf(element))) keys.add(key)
  }
  return keys
}

/**
 * 按样例值或占位符替换文本里的绑定。样例值缺失时保留 `{{key}}`，方便看出哪里还没填样例。
 * @param text 文本。
 * @param fields 字段定义（取样例值、默认值和格式）。
 * @param mode 显示方式。
 * @returns 替换后的文本。
 */
export function resolveText(text: string, fields: readonly FieldDefinition[], mode: DisplayMode): string {
  if (mode === 'placeholder' || !text.includes('{{')) return text
  return text.replace(BINDING, (whole, key: string, format: string | undefined) => {
    const field = fields.find((item) => item.key === key)
    const value = field?.sample ?? field?.default
    if (value === undefined || value === null || value === '') return whole
    return formatValue(value, field?.type ?? 'string', format?.trim() || field?.format)
  })
}

/**
 * 按字段类型和格式把值转成显示文本。
 * @param value 值。
 * @param type 字段类型。
 * @param format 格式；不传时用默认格式。
 * @returns 显示文本。
 */
export function formatValue(value: unknown, type: FieldType, format?: string): string {
  if (value === undefined || value === null) return ''
  if (type === 'number') return formatNumber(Number(value), format)
  if (type === 'date') return formatDate(String(value), format)
  return String(value)
}

/**
 * .NET 数字格式的常用子集：0、0.00、#,##0.00、0.###、N2、F2、D6。
 * @param value 数字。
 * @param format .NET 数字格式；不传或认不出时原样输出。
 * @returns 格式化后的文本。
 */
export function formatNumber(value: number, format?: string): string {
  if (!Number.isFinite(value)) return String(value)
  if (!format) return String(value)
  const standard = /^([NnFfDd])(\d{0,2})$/.exec(format)
  if (standard) {
    const kind = standard[1].toUpperCase()
    const digits = standard[2] === '' ? (kind === 'D' ? 0 : 2) : Number(standard[2])
    if (kind === 'D') return Math.trunc(value).toString().padStart(digits, '0')
    const fixed = value.toFixed(digits)
    return kind === 'N' ? groupThousands(fixed) : fixed
  }

  const custom = /^([#0,]*)(?:\.([0#]*))?$/.exec(format)
  if (!custom) return String(value)
  const integerPattern = custom[1] ?? ''
  const decimalPattern = custom[2] ?? ''
  const minDecimals = (decimalPattern.match(/0/g) ?? []).length
  let text = value.toFixed(decimalPattern.length)
  if (decimalPattern.length > minDecimals) {
    const [whole, decimals = ''] = text.split('.')
    let trimmed = decimals
    while (trimmed.length > minDecimals && trimmed.endsWith('0')) trimmed = trimmed.slice(0, -1)
    text = trimmed ? `${whole}.${trimmed}` : whole
  }

  const minIntegers = (integerPattern.match(/0/g) ?? []).length
  const negative = text.startsWith('-')
  const [whole, decimals] = (negative ? text.slice(1) : text).split('.')
  let integer = whole.padStart(minIntegers, '0')
  if (minIntegers === 0 && integer === '0' && decimals) integer = ''
  let result = decimals === undefined ? integer : `${integer}.${decimals}`
  if (integerPattern.includes(',')) result = groupThousands(result)
  return (negative ? '-' : '') + result
}

/**
 * 整数部分每三位加逗号。
 * @param text 数字文本（可以带小数部分）。
 * @returns 加了千分位的文本。
 */
function groupThousands(text: string): string {
  const [whole, decimals] = text.split('.')
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  return decimals === undefined ? grouped : `${grouped}.${decimals}`
}

/**
 * 解析 ISO 8601 日期或日期时间，按调用方写的钟面时间显示，不换算时区。
 * @param text 日期文本，如 2026-10-08、2026-10-08T14:30:00。
 * @returns 本地时间的 Date；格式不对时为 null。
 */
export function parseDate(text: string): Date | null {
  const match = /^(\d{4})-(\d{1,2})-(\d{1,2})(?:[T ](\d{1,2}):(\d{2})(?::(\d{2}))?)?/.exec(text.trim())
  if (!match) return null
  const date = new Date(+match[1], +match[2] - 1, +match[3], +(match[4] ?? 0), +(match[5] ?? 0), +(match[6] ?? 0))
  return Number.isNaN(date.getTime()) ? null : date
}

/**
 * .NET 日期格式的常用子集：yyyy、yy、MM、M、dd、d、HH、H、mm、ss；其他字符原样输出。
 * @param text 日期文本。
 * @param format .NET 日期格式；不传时按有没有时间选 yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss。
 * @returns 格式化后的文本；解析不了时原样返回。
 */
export function formatDate(text: string, format?: string): string {
  const date = parseDate(text)
  if (!date) return text
  const hasTime = date.getHours() !== 0 || date.getMinutes() !== 0 || date.getSeconds() !== 0
  const pattern = format || (hasTime ? 'yyyy-MM-dd HH:mm:ss' : 'yyyy-MM-dd')
  const pad = (n: number) => String(n).padStart(2, '0')
  const parts: Record<string, string> = {
    yyyy: String(date.getFullYear()),
    yy: String(date.getFullYear()).slice(2),
    MM: pad(date.getMonth() + 1),
    M: String(date.getMonth() + 1),
    dd: pad(date.getDate()),
    d: String(date.getDate()),
    HH: pad(date.getHours()),
    H: String(date.getHours()),
    mm: pad(date.getMinutes()),
    ss: pad(date.getSeconds()),
  }
  return pattern.replace(/yyyy|yy|MM|M|dd|d|HH|H|mm|ss/g, (token) => parts[token])
}

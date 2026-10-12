/**
 * 字段的自动值（ADR 0012）：调用方没传时由服务端算出来。这里是设计器用的预设、写法解析和预览值，规则和后端 AutoValues 一致。
 */
import type { FieldDefinition, FieldType } from './template.ts'

/**
 * 一个自动值预设。
 */
export interface AutoPreset {
  /**
   * 预设的写法；带 N 的写成 today+Nd 这样，N 由用户填。
   */
  kind: string
  /**
   * 下拉框里的说明。
   */
  label: string
  /**
   * 带 N 时的默认 N。
   */
  defaultN?: number
}

/**
 * 按字段类型的预设。图片没有自动值。
 */
export const AUTO_PRESETS: Record<FieldType, AutoPreset[]> = {
  date: [
    { kind: 'today', label: '当前日期' },
    { kind: 'now', label: '当前日期和时间' },
    { kind: 'today+Nd', label: 'N 天后', defaultN: 30 },
    { kind: 'today-Nd', label: 'N 天前', defaultN: 1 },
    { kind: 'today+Nm', label: 'N 个月后', defaultN: 6 },
    { kind: 'today+Ny', label: 'N 年后', defaultN: 1 },
  ],
  number: [
    { kind: 'index', label: '批量序号（第几张）' },
    { kind: 'total', label: '本批共几张' },
  ],
  string: [
    { kind: 'client', label: '调用方名称' },
    { kind: 'requestId', label: '请求号' },
  ],
  image: [],
}

/**
 * today±N(d|m|y) 的写法，N 为 1～9999。
 */
const RELATIVE = /^today([+-])([1-9]\d{0,3})([dmy])$/

/**
 * 预览时调用方名称、请求号显示的示意文字（和后端预览一致）。
 */
const PREVIEW_TEXT: Record<string, string> = { client: '示例调用方', requestId: 'PREVIEW' }

/**
 * 解析自动值写法。
 * @param auto 写法，如 today+30d。
 * @returns 预设的 kind 和 N（不带 N 的预设没有 n）；不认识时为 null。
 */
export function parseAuto(auto: string): { kind: string; n?: number } | null {
  const match = RELATIVE.exec(auto)
  if (match) return { kind: `today${match[1]}N${match[3]}`, n: Number(match[2]) }
  return Object.values(AUTO_PRESETS).flat().some((preset) => preset.kind === auto && !preset.kind.includes('N')) ? { kind: auto } : null
}

/**
 * 按预设和 N 拼出写法。
 * @param kind 预设的 kind。
 * @param n 带 N 的预设用的 N；不在 1～9999 时用预设的默认值。
 * @returns 写法，如 today+30d。
 */
export function buildAuto(kind: string, n?: number): string {
  if (!kind.includes('N')) return kind
  const preset = Object.values(AUTO_PRESETS).flat().find((item) => item.kind === kind)
  const value = n !== undefined && Number.isInteger(n) && n >= 1 && n <= 9999 ? n : (preset?.defaultN ?? 1)
  return kind.replace('N', String(value))
}

/**
 * 写法认不认识、和字段类型是否匹配（发布前检查用，规则和后端一致）。
 * @param type 字段类型。
 * @param auto 写法。
 * @returns 能用时为 true。
 */
export function isValidAuto(type: FieldType, auto: string): boolean {
  const parsed = parseAuto(auto)
  return !!parsed && AUTO_PRESETS[type].some((preset) => preset.kind === parsed.kind)
}

/**
 * 写法的中文说明，如“今天 + 30 天”。
 * @param auto 写法。
 * @returns 说明；不认识时原样返回。
 */
export function describeAuto(auto: string): string {
  const parsed = parseAuto(auto)
  if (!parsed) return auto
  if (parsed.n === undefined) return Object.values(AUTO_PRESETS).flat().find((preset) => preset.kind === auto)?.label ?? auto
  const unit = { d: '天', m: '个月', y: '年' }[parsed.kind.slice(-1) as 'd' | 'm' | 'y']
  return `今天 ${parsed.kind.includes('+') ? '+' : '-'} ${parsed.n} ${unit}`
}

/**
 * 补两位。
 * @param value 数字。
 * @returns 如 08。
 */
const pad = (value: number) => String(value).padStart(2, '0')

/**
 * 加减月份，遇到月底取当月最后一天（和 .NET 的 AddMonths 一样：1 月 31 日加 1 个月是 2 月 28 日）。
 * @param date 日期。
 * @param months 月数（可为负）。
 * @returns 新的日期。
 */
export function addMonths(date: Date, months: number): Date {
  const first = new Date(date.getFullYear(), date.getMonth() + months, 1)
  const lastDay = new Date(first.getFullYear(), first.getMonth() + 1, 0).getDate()
  return new Date(first.getFullYear(), first.getMonth(), Math.min(date.getDate(), lastDay))
}

/**
 * 设计器预览用的值：按浏览器当前时间算；序号、总数是 1，调用方、请求号用示意文字。
 * @param field 有自动值的字段。
 * @param now 当前时间（检查程序里固定它）。
 * @returns 和调用方传值一样的写法（日期是 ISO 文本）；没有自动值或写法不认识时为 undefined。
 */
export function autoPreview(field: Pick<FieldDefinition, 'type' | 'auto'>, now = new Date()): unknown {
  if (!field.auto || !isValidAuto(field.type, field.auto)) return undefined
  const date = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const parsed = parseAuto(field.auto)!
  if (field.auto === 'now') return `${date(now)}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`
  if (field.auto === 'today') return date(today)
  if (field.auto === 'index' || field.auto === 'total') return 1
  if (parsed.n === undefined) return PREVIEW_TEXT[field.auto]
  const n = field.auto.includes('+') ? parsed.n : -parsed.n
  const unit = field.auto.slice(-1)
  if (unit === 'd') return date(new Date(today.getFullYear(), today.getMonth(), today.getDate() + n))
  return date(addMonths(today, unit === 'm' ? n : n * 12))
}

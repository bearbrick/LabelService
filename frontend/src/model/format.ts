/**
 * 后台页面的显示格式：时间、画布尺寸、文件大小。时间参数是 ISO 字符串（UTC），按本地时区显示。
 */

const pad2 = (value: number) => String(value).padStart(2, '0')

/** 相对时间：刚刚、N 分钟前、今天 HH:mm、M-dd HH:mm。 */
export function fmtTime(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return '—'
  const time = Date.parse(iso)
  if (Number.isNaN(time)) return '—'
  const diff = now - time
  if (diff < 60e3) return '刚刚'
  if (diff < 3600e3) return `${Math.floor(diff / 60e3)} 分钟前`
  const date = new Date(time)
  const today = new Date(now)
  today.setHours(0, 0, 0, 0)
  if (time >= today.getTime()) return `今天 ${pad2(date.getHours())}:${pad2(date.getMinutes())}`
  return `${date.getMonth() + 1}-${pad2(date.getDate())} ${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

/** 完整时间 yyyy-MM-dd HH:mm:ss。 */
export function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return '—'
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())} ${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`
}

/** A4、A5、A6 纸张名（含横向、纵向），不是标准纸张返回空字符串。 */
export function paperName(page: { width: number; height: number }): string {
  const short = Math.min(page.width, page.height)
  const long = Math.max(page.width, page.height)
  const paper = ([['A4', 210, 297], ['A5', 148, 210], ['A6', 105, 148]] as const).find(([, a, b]) => a === short && b === long)
  return paper ? paper[0] + (page.width > page.height ? ' 横向' : ' 纵向') : ''
}

/** 画布尺寸：A4 纵向 · 210 × 297 mm。 */
export function sizeText(page: { width: number; height: number }): string {
  const name = paperName(page)
  return (name ? `${name} · ` : '') + `${page.width} × ${page.height} mm`
}

/** 文件大小：B、KB、MB。 */
export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

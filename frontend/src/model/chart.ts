/**
 * 工作台“近 14 天调用次数”的柱状图几何：成功在下、失败叠在上，顶端圆角，刻度取 1、2、5 的整倍数。
 * 只算坐标和路径，画在 SVG 里（viewBox 700×240）。
 */

/**
 * 一天的调用次数。
 */
export interface DayCount {
  /**
   * 日期 yyyy-MM-dd。
   */
  date: string
  /**
   * 成功次数。
   */
  ok: number
  /**
   * 失败次数。
   */
  fail: number
}

/**
 * 一天的柱子：SVG 路径、标签和提示框位置。
 */
export interface Bar {
  /**
   * 日期，作 v-for 的 key。
   */
  key: string
  /**
   * 柱子左边的 x（viewBox 坐标）。
   */
  x: number
  /**
   * 柱子宽度。
   */
  w: number
  /**
   * 柱子两侧的空白（每天占的宽度减柱宽后的一半），悬停区域用。
   */
  pad: number
  /**
   * 成功部分的路径；没有成功调用时为空。
   */
  okPath: string
  /**
   * 失败部分的路径（叠在成功上面）；没有失败时为空。
   */
  failPath: string
  /**
   * 成功次数。
   */
  ok: number
  /**
   * 失败次数。
   */
  fail: number
  /**
   * 横轴标签，如 10/8。
   */
  label: string
  /**
   * 提示框里的完整日期，如“10 月 8 日 星期四”。
   */
  full: string
  /**
   * 提示框的水平位置（相对图表宽度的百分比）。
   */
  tipLeft: string
  /**
   * 提示框的垂直位置（柱顶，相对图表高度的百分比）。
   */
  tipTop: string
}

/**
 * 图表尺寸：viewBox 宽高 W、H，左右上下留白 L、R、T、B。
 */
export const CHART = { W: 700, H: 240, L: 44, R: 12, T: 14, B: 28 } as const

/**
 * 星期几的中文写法，按 Date.getDay() 取。
 */
const WEEK = '日一二三四五六'

/**
 * 不小于 value 的“好看”步长：1、2、5、10 乘 10 的幂。
 * @param value 最小步长。
 * @returns 好看的步长。
 */
export function niceStep(value: number): number {
  const power = Math.pow(10, Math.floor(Math.log10(Math.max(value, 1e-9))))
  for (const m of [1, 2, 5, 10]) if (m * power >= value) return m * power
  return 10 * power
}

/**
 * 直角矩形的路径。
 * @param x 左边。
 * @param y 上边。
 * @param w 宽度。
 * @param h 高度。
 * @returns SVG 路径。
 */
const rectPath = (x: number, y: number, w: number, h: number) => `M${x} ${y}h${w}v${h}h${-w}z`

/**
 * 顶端圆角、底端直角的矩形路径。
 * @param x 左边。
 * @param y 上边。
 * @param w 宽度。
 * @param h 高度。
 * @param r 圆角半径（不超过高度和半个宽度）。
 * @returns SVG 路径。
 */
function roundTop(x: number, y: number, w: number, h: number, r: number): string {
  r = Math.max(0, Math.min(r, h, w / 2))
  return `M${x} ${y + h}V${y + r}Q${x} ${y} ${x + r} ${y}H${x + w - r}Q${x + w} ${y} ${x + w} ${y + r}V${y + h}z`
}

/**
 * 算柱状图：纵轴分 4 档左右取好看的刻度，每天一根柱子，成功在下、失败在上（中间留 2 的缝）。
 * @param days 每天的调用次数，按日期顺序。
 * @returns 刻度（值和 y）和柱子。
 */
export function barChart(days: readonly DayCount[]): { ticks: { v: number; y: number }[]; bars: Bar[] } {
  const { W, H, L, R, T, B } = CHART
  const base = H - B
  const max = Math.max(1, ...days.map((d) => d.ok + d.fail))
  const step = Math.max(1, niceStep(max / 4))
  const top = Math.ceil(max / step) * step
  const y = (v: number) => T + (base - T) * (1 - v / top)
  const ticks: { v: number; y: number }[] = []
  for (let v = 0; v <= top + 1e-9; v += step) ticks.push({ v, y: y(v) })
  const slot = (W - L - R) / Math.max(1, days.length)
  const w = Math.min(26, slot * 0.62)
  const pad = (slot - w) / 2
  const bars = days.map((d, i) => {
    const x = L + i * slot + pad
    const okTop = y(d.ok)
    const failTop = y(d.ok + d.fail)
    const okPath = d.ok ? (d.fail ? rectPath(x, okTop, w, base - okTop) : roundTop(x, okTop, w, base - okTop, 3)) : ''
    let failPath = ''
    if (d.fail) {
      const bottom = d.ok ? okTop - 2 : base
      const h = Math.max(1.5, bottom - failTop)
      failPath = roundTop(x, bottom - h, w, h, Math.min(3, h / 2))
    }

    const [year, month, day] = d.date.split('-').map(Number)
    const date = new Date(year, month - 1, day)
    return {
      key: d.date,
      x,
      w,
      pad,
      okPath,
      failPath,
      ok: d.ok,
      fail: d.fail,
      label: `${month}/${day}`,
      full: `${month} 月 ${day} 日 星期${WEEK[date.getDay()]}`,
      tipLeft: `${((x + w / 2) / W) * 100}%`,
      tipTop: `${(Math.min(failTop, okTop) / H) * 100}%`,
    }
  })
  return { ticks, bars }
}

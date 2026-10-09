/**
 * 工作台“近 14 天调用次数”的柱状图几何：成功在下、失败叠在上，顶端圆角，刻度取 1、2、5 的整倍数。
 * 只算坐标和路径，画在 SVG 里（viewBox 700×240）。
 */

export interface DayCount {
  date: string
  ok: number
  fail: number
}

export interface Bar {
  key: string
  x: number
  w: number
  pad: number
  okPath: string
  failPath: string
  ok: number
  fail: number
  label: string
  full: string
  tipLeft: string
  tipTop: string
}

export const CHART = { W: 700, H: 240, L: 44, R: 12, T: 14, B: 28 } as const
const WEEK = '日一二三四五六'

/** 不小于 value 的“好看”步长：1、2、5、10 乘 10 的幂。 */
export function niceStep(value: number): number {
  const power = Math.pow(10, Math.floor(Math.log10(Math.max(value, 1e-9))))
  for (const m of [1, 2, 5, 10]) if (m * power >= value) return m * power
  return 10 * power
}

const rectPath = (x: number, y: number, w: number, h: number) => `M${x} ${y}h${w}v${h}h${-w}z`

function roundTop(x: number, y: number, w: number, h: number, r: number): string {
  r = Math.max(0, Math.min(r, h, w / 2))
  return `M${x} ${y + h}V${y + r}Q${x} ${y} ${x + r} ${y}H${x + w - r}Q${x + w} ${y} ${x + w} ${y + r}V${y + h}z`
}

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

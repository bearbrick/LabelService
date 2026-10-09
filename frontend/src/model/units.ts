/**
 * 单位换算，和后端 backend/src/LabelService.Contracts/LabelUnits.cs 用同样的公式。
 */

export const MM_PER_INCH = 25.4
export const POINTS_PER_INCH = 72

/** 毫米换算成打印点：点数 = 毫米 ÷ 25.4 × DPI。 */
export function mmToDots(mm: number, dpi: number): number {
  return (mm / MM_PER_INCH) * dpi
}

/** 磅换算成毫米：1pt ≈ 0.353mm。 */
export function pointsToMm(points: number): number {
  return (points * MM_PER_INCH) / POINTS_PER_INCH
}

/** 模板里的长度保留 1 位小数（0.1mm）。 */
export function roundMm(mm: number): number {
  return Math.round(mm * 10) / 10
}

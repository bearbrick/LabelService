/**
 * 单位换算，和后端 backend/src/LabelService.Contracts/LabelUnits.cs 用同样的公式。
 */

/**
 * 1 英寸的毫米数。
 */
export const MM_PER_INCH = 25.4

/**
 * 1 英寸的磅数。
 */
export const POINTS_PER_INCH = 72

/**
 * 1pt 等于多少 mm（≈ 0.353）。
 */
export const MM_PER_PT = MM_PER_INCH / POINTS_PER_INCH

/**
 * 设计器 100% 缩放时每 mm 的屏幕像素：按 96dpi 显示，屏幕上看到的就是实际尺寸。
 */
export const BASE_PX_PER_MM = 96 / MM_PER_INCH

/**
 * 毫米换算成打印点：点数 = 毫米 ÷ 25.4 × DPI。
 * @param mm 毫米。
 * @param dpi 分辨率。
 * @returns 打印点（不取整）。
 */
export function mmToDots(mm: number, dpi: number): number {
  return (mm / MM_PER_INCH) * dpi
}

/**
 * 磅换算成毫米：1pt ≈ 0.353mm。
 * @param points 磅。
 * @returns 毫米。
 */
export function pointsToMm(points: number): number {
  return points * MM_PER_PT
}

/**
 * 模板里的长度保留 1 位小数（0.1mm）。非数字按 0 处理。
 * @param mm 毫米。
 * @returns 保留 1 位小数的毫米。
 */
export function roundMm(mm: number): number {
  const value = Number(mm)
  return Number.isFinite(value) ? Math.round(value * 10) / 10 : 0
}

/**
 * 角度归一到 [0, 360)，保留 1 位小数。
 * @param degrees 角度。
 * @returns 0 到 360（不含）之间的角度。
 */
export function normalizeRotation(degrees: number): number {
  const value = Number(degrees) || 0
  return roundMm(((value % 360) + 360) % 360) % 360
}

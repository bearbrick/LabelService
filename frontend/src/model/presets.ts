/**
 * 画布预设。管理后台（T-014）完成后改为从服务端读取，管理员可以增删。
 */
export interface CanvasPreset {
  name: string
  width: number
  height: number
}

export const CANVAS_PRESETS: readonly CanvasPreset[] = [
  { name: 'A4', width: 210, height: 297 },
  { name: 'A5', width: 148, height: 210 },
  { name: '100×150', width: 100, height: 150 },
  { name: '100×100', width: 100, height: 100 },
  { name: '100×60', width: 100, height: 60 },
  { name: '80×50', width: 80, height: 50 },
  { name: '60×40', width: 60, height: 40 },
  { name: '50×30', width: 50, height: 30 },
]

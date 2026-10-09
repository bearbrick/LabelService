/**
 * 把画布上的纸张导出成白底图片（只有元素，没有选择框、参考线、网格），和服务端预览对比用（T-016）。
 */
import Konva from 'konva'
import type { LabelPage } from '../model/template.ts'

/** 按指定像素尺寸导出；画布还没创建时返回 null。元素层在 stage.ts 里命名为 elements。 */
export function snapshotPage(container: HTMLElement, page: LabelPage, width: number, height: number): HTMLCanvasElement | null {
  const stage = Konva.stages.find((item) => item.container() === container)
  const layer = stage?.findOne<Konva.Layer>('.elements')
  if (!stage || !layer) return null
  const scale = stage.scaleX()
  const image = layer.toCanvas({ x: stage.x(), y: stage.y(), width: page.width * scale, height: page.height * scale, pixelRatio: width / (page.width * scale) })
  const output = document.createElement('canvas')
  output.width = width
  output.height = height
  const context = output.getContext('2d')!
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, width, height)
  context.drawImage(image, 0, 0, width, height)
  return output
}

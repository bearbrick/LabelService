/**
 * 设计器的模板级操作：打开模板（清空撤销历史）、导入和导出模板 JSON、修改画布尺寸。
 */
import type { LabelTemplate } from '../model/template.ts'
import { roundMm } from '../model/units.ts'
import { commit, resetHistory } from './designerHistory.ts'
import { copyOf, designer, report, requestFit, zoomFit } from './designerState.ts'

/**
 * 打开一个模板（从服务端读到的草稿或版本），清空撤销历史。readOnly 为真时不能修改。
 * @param template 模板。
 * @param readOnly 只读。
 */
export function openTemplate(template: LabelTemplate, readOnly = false): void {
  designer.template = copyOf(template)
  designer.selection = []
  designer.metrics = {}
  designer.readOnly = readOnly
  designer.dirty = false
  designer.saveError = ''
  designer.savedAt = ''
  // 丢掉还没执行的合并和保存，以新模板作为撤销历史的起点。
  resetHistory()
  requestFit()
}

/**
 * 导入模板 JSON 文本，替换当前模板的画布、字段和元素（编码和名称不变，可以撤销）；格式不对时抛出带中文说明的错误。
 * @param json 模板 JSON 文本。
 */
export function importTemplate(json: string): void {
  let parsed: Partial<LabelTemplate>
  try {
    parsed = JSON.parse(json) as Partial<LabelTemplate>
  } catch {
    throw new Error('不是合法的 JSON')
  }

  if (!parsed.page || !(parsed.page.width > 0) || !(parsed.page.height > 0) || !Array.isArray(parsed.elements)) {
    throw new Error('缺少 page 或 elements，不是模板 JSON')
  }

  const imported = copyOf({ schemaVersion: '1.0', code: '', name: '', fields: [], ...parsed } as LabelTemplate)
  designer.template.page = imported.page
  designer.template.fields = imported.fields
  designer.template.elements = imported.elements
  designer.selection = []
  commit()
  requestFit()
}

/**
 * 导出模板 JSON（缩进 2 格）。有不能导出的问题时抛错。
 * @returns JSON 文本。
 */
export function exportTemplate(): string {
  const problems = report.value.errors
  if (problems.length) throw new Error(problems[0])
  return JSON.stringify(designer.template, null, 2)
}

/**
 * 修改画布尺寸（保留 1 位小数），记录撤销并重新适应窗口。
 * @param width 宽度，mm。
 * @param height 高度，mm。
 */
export function setPage(width: number, height: number): void {
  designer.template.page.width = roundMm(width)
  designer.template.page.height = roundMm(height)
  commit()
  zoomFit()
}

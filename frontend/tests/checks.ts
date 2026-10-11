/**
 * 前端检查程序：用 Node 直接运行（类型擦除），检查不依赖浏览器的模块。任何一项失败抛异常，退出码非 0。
 * 运行：npm run check
 */
import sampleJson from '../src/samples/box-label.json' with { type: 'json' }
import { alignElements, distributeElements, moveAbove, reorder } from '../src/editor/arrange.ts'
import { encode, moduleDots } from '../src/editor/barcode.ts'
import { readClipboard, writeClipboard } from '../src/editor/clipboard.ts'
import { contains, elementBounds, intersects } from '../src/editor/geometry.ts'
import { History } from '../src/editor/history.ts'
import { PLACEHOLDER_IMAGE, sampleRequest } from '../src/model/request.ts'
import { fileSize, fmtTime, sizeText } from '../src/model/format.ts'
import { barChart, niceStep } from '../src/model/chart.ts'
import { gridStep, transformRules } from '../src/editor/overlays.ts'
import { compareElements, inkEdges } from '../src/editor/compare.ts'
import { snapBox } from '../src/editor/snapping.ts'
import { formatDate, formatNumber, keysIn, renameKey, resolveText } from '../src/model/bindings.ts'
import { checkLayout, footprint, normalizeLayout, pageCount } from '../src/model/layout.ts'
import { arrange, fitGrid, moveSlot, newLayout, renumber, suggestCode } from '../src/model/layoutArrange.ts'
import type { SheetLayout } from '../src/model/layout.ts'
import { cloneElements, makeElement, normalizeElement } from '../src/model/elements.ts'
import type { FieldDefinition, LabelElement, LabelTemplate, RectElement } from '../src/model/template.ts'
import { normalizeRotation, roundMm } from '../src/model/units.ts'
import { checkTemplate } from '../src/model/validate.ts'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { checkDocs, sourceFiles } from './docs.ts'

/**
 * 通过的检查项数。
 */
let passed = 0

/**
 * 记录一项检查：通过时打印 PASS，失败时抛异常结束检查（退出码非 0）。
 * @param name 检查项的中文描述。
 * @param value 是否通过。
 */
function check(name: string, value: boolean): void {
  if (!value) throw new Error(`FAIL: ${name}`)
  console.log(`PASS: ${name}`)
  passed++
}

/**
 * 示例箱标模板（和 backend/samples/templates/box-label.json 一致）。
 */
const sample = sampleJson as unknown as LabelTemplate

/**
 * 检查用的字段：各种类型、有无格式、有无样例值。
 */
const fields: FieldDefinition[] = [
  { key: 'batchNo', type: 'string', sample: '20261008-A' },
  { key: 'qty', type: 'number', format: '0', sample: 50 },
  { key: 'price', type: 'number', sample: 12.5 },
  { key: 'prodDate', type: 'date', format: 'yyyy-MM-dd', sample: '2026-10-08' },
  { key: 'when', type: 'date', sample: '2026-10-08T14:30:00' },
  { key: 'noSample', type: 'string' },
]

/**
 * 绑定替换：样例值、占位符、字段格式和绑定格式、.NET 数字和日期格式的子集。
 */
function checkBindings(): void {
  check('keysIn 去重并保持顺序', keysIn('{{a}}-{{ b:0 }}-{{a}}').join(',') === 'a,b')
  check('样例值替换', resolveText('批次：{{batchNo}}', fields, 'sample') === '批次：20261008-A')
  check('占位符模式原样显示', resolveText('批次：{{batchNo}}', fields, 'placeholder') === '批次：{{batchNo}}')
  check('字段格式生效', resolveText('{{qty}} PCS', fields, 'sample') === '50 PCS')
  check('绑定格式优先', resolveText('{{price:0.00}}', fields, 'sample') === '12.50')
  check('日期绑定格式', resolveText('{{prodDate:yyyy/MM/dd}}', fields, 'sample') === '2026/10/08')
  check('日期时间默认格式', resolveText('{{when}}', fields, 'sample') === '2026-10-08 14:30:00')
  check('没有样例值时保留占位符', resolveText('[{{noSample}}]', fields, 'sample') === '[{{noSample}}]')
  check('数字格式：千分位', formatNumber(1234567.891, '#,##0.00') === '1,234,567.89')
  check('数字格式：可选小数', formatNumber(1.5, '0.###') === '1.5' && formatNumber(2, '0.##') === '2')
  check('数字格式：N2、D6', formatNumber(1234.5, 'N2') === '1,234.50' && formatNumber(42, 'D6') === '000042')
  check('日期格式：带时区按钟面时间', formatDate('2026-10-08T23:30:00+08:00', 'yyyy-MM-dd HH:mm') === '2026-10-08 23:30')
}

/**
 * 新建、补全默认值、复制元素。
 */
function checkElements(): void {
  const created = makeElement('rect', [], [])
  check('新建矩形有 0.3mm 边框', created.type === 'rect' && created.stroke?.width === 0.3)
  const loaded = normalizeElement({ id: 'r', type: 'rect', x: 0, y: 0, width: 10, height: 10, fill: '#000000' }) as RectElement
  check('打开的矩形没设 stroke 时保持无边框', loaded.stroke?.width === 0)
  const line = normalizeElement({ id: 'l', type: 'line', x: 0, y: 0, width: 10, height: 0 })
  check('线不设 stroke 时按默认线宽', line.type === 'line' && line.stroke?.width === 0.3)
  const barcode = makeElement('barcode', fields, [])
  check('新建一维码绑定第一个非图片字段', barcode.type === 'barcode' && barcode.value === '{{batchNo}}')
  const text = makeElement('text', [], [])
  check('新建文本补全样式默认值', text.type === 'text' && text.style?.fontFamily === 'SourceHanSansSC' && text.style.fontSize === 10)
  const copies = cloneElements([{ ...text, locked: true }], 2, [text])
  check('复制元素：新 ID、偏移 2mm、解除锁定', copies[0].id !== text.id && copies[0].x === roundMm(text.x + 2) && copies[0].locked === false)
}

/**
 * 撤销历史：相同快照不入栈、上限、重做、只读撤回。
 */
function checkHistory(): void {
  const history = new History(3)
  history.reset('a')
  check('相同快照不入栈', history.push('a') === false)
  history.push('b')
  history.push('c')
  history.push('d')
  check('超过上限丢掉最早的', history.undo() === 'c' && history.undo() === 'b' && history.undo() === null)
  check('撤销后可以重做', history.redo() === 'c' && history.canRedo)
  history.push('x')
  check('撤销后再修改，丢掉后面的重做', !history.canRedo)
  history.undo()
  history.dropRedo()
  check('只读撤回后不能重做出来', !history.canRedo)
}

/**
 * 几何计算和拖动吸附：旋转后的外框、相交、框选包含、参考线和网格吸附。
 */
function checkGeometry(): void {
  const rotated = elementBounds({ x: 10, y: 30, width: 20, height: 0, rotation: 90 })
  check('横线旋转 90° 后外框变成竖条', Math.abs(rotated.x - 20) < 1e-9 && Math.abs(rotated.height - 20) < 1e-9)
  check('宽为 0 的线也算相交', intersects({ x: 5, y: 0, width: 0, height: 10 }, { x: 0, y: 0, width: 10, height: 10 }))
  check('框选只算完全包住的元素', contains({ x: 0, y: 0, width: 10, height: 10 }, { x: 5, y: 0, width: 0, height: 10 }) && !contains({ x: 2, y: 2, width: 5, height: 5 }, { x: 0, y: 0, width: 10, height: 10 }))
  const page = { width: 100, height: 60 }
  const toCenter = snapBox({ x: 39.2, y: 10, width: 20, height: 5 }, [], page, { threshold: 1, grid: null })
  check('吸附到画布中线', Math.abs(toCenter.dx - 0.8) < 1e-9 && toCenter.guideX === 50)
  const toOther = snapBox({ x: 30.4, y: 20.3, width: 10, height: 5 }, [{ x: 30, y: 0, width: 5, height: 5 }], page, { threshold: 1, grid: 1 })
  check('吸附到其他元素左边，纵向退回网格', Math.abs(toOther.dx + 0.4) < 1e-9 && Math.abs(toOther.dy + 0.3) < 1e-9 && toOther.guideY === null)
}

/**
 * 对齐、等间距分布、叠放顺序、图层拖动。
 */
function checkArrange(): void {
  const page = { width: 100, height: 60 }
  const boxes = (): LabelElement[] => [
    { id: 'a', type: 'rect', x: 0, y: 0, width: 10, height: 10 },
    { id: 'b', type: 'rect', x: 15, y: 5, width: 10, height: 10, locked: true },
    { id: 'c', type: 'rect', x: 50, y: 20, width: 10, height: 10 },
  ]
  const left = boxes()
  alignElements(left, 'left', page)
  check('左对齐：锁定的不动', left[0].x === 0 && left[1].x === 15 && left[2].x === 0)
  const single = boxes().slice(0, 1)
  alignElements(single, 'hcenter', page)
  check('只选一个时对齐到画布', single[0].x === 45)
  const spread = boxes().map((element) => ({ ...element, locked: false }))
  distributeElements(spread, 'h')
  check('水平等距', spread[1].x === 25)
  const ids = new Set(['a'])
  check('置顶、置底', reorder(boxes(), ids, 'front').map((e) => e.id).join('') === 'bca' && reorder(boxes(), new Set(['c']), 'back')[0].id === 'c')
  check('上移、下移一层', reorder(boxes(), ids, 'up').map((e) => e.id).join('') === 'bac' && reorder(boxes(), new Set(['c']), 'down').map((e) => e.id).join('') === 'acb')
  check('图层拖动：放到目标上一层', moveAbove(boxes(), 'a', 'c').map((e) => e.id).join('') === 'bca')
}

/**
 * 剪贴板往返和角度归一。
 */
function checkClipboard(): void {
  const text = makeElement('text', [], [])
  writeClipboard([text])
  check('剪贴板往返', readClipboard()?.[0].id === text.id)
  check('角度归一', normalizeRotation(-90) === 270 && normalizeRotation(359.96) === 0)
}

/**
 * 条码编码（bwip-js）和模块宽度取整。
 */
function checkBarcodes(): void {
  const code128 = encode('code128', 'C2610080001')
  check('Code128 编码', code128.ok && code128.code.kind === 'linear' && code128.code.modules > 50)
  const badEan = encode('ean13', 'ABC')
  check('EAN-13 内容不对时给出中文提示', !badEan.ok && /数字/.test(badEan.message))
  const qr = encode('qr', '3100-0123|20261008-A|C2610080001', 'M')
  check('QR 编码', qr.ok && qr.code.kind === 'matrix' && qr.code.columns === qr.code.rows)
  check('模块宽度按打印点向下取整', moduleDots(64, 145, 300) === 5 && moduleDots(64, 145, 203) === 3)
}

/**
 * 字段改名、调用样例、后台的显示格式（尺寸、相对时间、文件大小）、工作台柱状图。
 */
function checkFieldsAndFormats(): void {
  check('改字段 key 保留格式、不动别的字段', renameKey('{{qty}}箱 {{ qty:0.00 }} {{qtyMax}}', 'qty', 'count') === '{{count}}箱 {{ count:0.00 }} {{qtyMax}}')
  const request = sampleRequest({
    code: 'DEMO',
    fields: [
      { key: 'name', type: 'string', sample: '电源' },
      { key: 'qty', type: 'number' },
      { key: 'logo', type: 'image' },
      { key: 'note', type: 'string', required: false },
      { key: 'lot', type: 'string', default: 'A' },
    ],
  })
  const data = request.data as Record<string, unknown>
  check('调用样例：有样例用样例，必填没样例给占位，可选和有默认值的不传', request.templateCode === 'DEMO' && data.name === '电源' && data.qty === 0 && data.logo === PLACEHOLDER_IMAGE && !('note' in data) && !('lot' in data))

  check('画布尺寸带纸张名', sizeText({ width: 297, height: 210 }) === 'A4 横向 · 297 × 210 mm' && sizeText({ width: 100, height: 60 }) === '100 × 60 mm')
  const now = Date.parse('2026-10-09T08:00:00Z')
  check('相对时间', fmtTime('2026-10-09T07:59:30Z', now) === '刚刚' && fmtTime('2026-10-09T07:30:00Z', now) === '30 分钟前' && fmtTime(null, now) === '—')
  check('文件大小', fileSize(512) === '512 B' && fileSize(2048) === '2.0 KB' && fileSize(3 * 1024 * 1024) === '3.00 MB')
  check('图表刻度取 1、2、5 的整倍数', niceStep(3) === 5 && niceStep(12) === 20 && niceStep(0.4) === 0.5)
  const chart = barChart([{ date: '2026-10-08', ok: 37, fail: 3 }, { date: '2026-10-09', ok: 0, fail: 0 }])
  check('柱状图：刻度盖住最大值、失败叠在成功上、空的一天没有柱子', chart.ticks.at(-1)!.v >= 40 && chart.bars[0].okPath !== '' && chart.bars[0].failPath !== '' && chart.bars[1].okPath === '' && chart.bars[0].label === '10/8')
}

/**
 * 变换框的规则和网格间距。
 */
function checkTransform(): void {
  const hline = transformRules([{ ...makeElement('line', [], []), width: 30, height: 0 }])
  check('横线只能左右拉、不能旋转', hline.mode === 'hline' && !hline.rotate && hline.anchors.join() === 'middle-left,middle-right')
  const qrRules = transformRules([makeElement('qrcode', [], [])])
  check('二维码等比缩放，锁定后不能变换', qrRules.keepRatio && qrRules.anchors.length === 4 && transformRules([{ ...makeElement('rect', [], []), locked: true }]).locked)
  check('网格缩小到不足 6 像素时放大一档', gridStep(1, 10) === 1 && gridStep(1, 3) === 2 && gridStep(0.5, 0.01) === 100)
}

/**
 * 画布和服务端 PNG 的位置对比：墨迹外框、元素偏差。
 */
function checkCompare(): void {
  function blank(width: number, height: number): { data: Uint8ClampedArray; width: number; height: number } {
    return { data: new Uint8ClampedArray(width * height * 4).fill(255), width, height }
  }
  function ink(image: { data: Uint8ClampedArray; width: number }, x0: number, y0: number, x1: number, y1: number): void {
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) image.data.fill(0, (y * image.width + x) * 4, (y * image.width + x) * 4 + 3)
  }
  const inkA = blank(100, 60)
  const inkB = blank(100, 60)
  ink(inkA, 20, 20, 39, 29)
  ink(inkB, 22, 20, 41, 29)
  check('墨迹外框', JSON.stringify(inkEdges(inkA, { left: 0, top: 0, right: 99, bottom: 59 })) === JSON.stringify({ left: 20, top: 20, right: 39, bottom: 29 }))
  const offsets = compareElements([{ ...makeElement('rect', [], []), id: 'r1', x: 2, y: 2, width: 2, height: 1 }], inkA, inkB, 10)
  check('位置对比：错开 2 像素、每 mm 10 像素，偏差 0.2 mm', Math.abs(offsets[0].maxMm - 0.2) < 1e-9)
}

/**
 * 示例模板没有问题；不合法和重复的字段 key 能检查出来。
 */
function checkSampleTemplate(): void {
  const report = checkTemplate(sample)
  check('示例模板没有问题', report.errors.length === 0 && report.undefinedKeys.length === 0)
  const broken: LabelTemplate = { ...sample, fields: [...sample.fields, { key: '1bad', type: 'string' }, { key: 'qty', type: 'number' }] }
  const brokenReport = checkTemplate(broken)
  check('检查出不合法和重复的字段 key', brokenReport.errors.some((e) => e.includes('1bad')) && brokenReport.errors.some((e) => e.includes('qty') && e.includes('重复')))
}

/**
 * 拼版版式：新建、默认编码、旋转、自动排列、检查、重新编号、调整顺序、页数、取整。
 */
function checkLayouts(): void {
  const a4 = { width: 210, height: 297 }
  const box100x60 = { width: 100, height: 60 }
  const fresh = newLayout('A4_100x60_2x4', 'A4 · 100×60', a4, box100x60)
  check('新建版式按纸张排满：A4 放 100×60 是 2 列 × 4 行，整体居中，和服务端示例版式的坐标一样',
    fresh.slots.length === 8 && fresh.slots[0].x === 5 && fresh.slots[0].y === 28.5 && fresh.slots[1].x === 105 && fresh.slots[7].y === 208.5 && checkLayout(fresh).messages.length === 0)
  check('默认编码 A4_100x60_2x4（列 × 行）', suggestCode('A4 纵向', box100x60, 4, 2) === 'A4_100x60_2x4')
  check('旋转 90°、270° 时格子所占的宽高互换', footprint(box100x60, { x: 0, y: 0, rotation: 90 }).width === 60 && footprint(box100x60, { x: 0, y: 0, rotation: 180 }).width === 100)
  const rotatedGrid = fitGrid(a4, box100x60, true)
  check('竖着放：A4 能放 3 列 × 2 行', rotatedGrid.cols === 3 && rotatedGrid.rows === 2 && arrange(box100x60, { ...rotatedGrid, gapX: 0, gapY: 0, rotate: true, order: 'rows' }).every((slot) => slot.rotation === 90))
  check('先列后行排列：第二个格子在第一个下面', (() => {
    const slots = arrange(box100x60, { rows: 4, cols: 2, marginTop: 28.5, marginLeft: 5, gapX: 0, gapY: 0, rotate: false, order: 'cols' })
    return slots[1].x === 5 && slots[1].y === 88.5 && slots[4].x === 105
  })())

  const withProblems: SheetLayout = { ...fresh, slots: [{ x: 5, y: 10, rotation: 0 }, { x: 90, y: 10, rotation: 0 }, { x: 150, y: 250, rotation: 0 }, { x: 5, y: 100, rotation: 45 }] }
  const found = checkLayout(withProblems)
  check('版式检查：重叠、越界、旋转角度，带格子序号，和服务端的说法一致；出问题的格子标红',
    found.messages.includes('第 2 个格子和第 1 个格子重叠') && found.messages.includes('第 3 个格子超出了纸张') && found.messages.some((m) => m.startsWith('第 4 个格子的旋转'))
    && [0, 1, 2, 3].every((index) => found.badSlots.has(index)))
  check('贴边 0.05 mm 以内不算重叠', checkLayout({ ...fresh, slots: [{ x: 5, y: 10, rotation: 0 }, { x: 105.04, y: 10, rotation: 0 }] }).messages.length === 0)
  check('编码不合规、没有格子都报出来', checkLayout({ ...fresh, code: '1A', slots: [] }).messages.length === 2)

  const shuffled = [fresh.slots[5], fresh.slots[0], fresh.slots[3], fresh.slots[6], fresh.slots[1], fresh.slots[7], fresh.slots[2], fresh.slots[4]]
  check('重新编号：先行后列恢复成一行一行从左到右', JSON.stringify(renumber(shuffled, 'rows')) === JSON.stringify(fresh.slots))
  const byColumns = renumber(shuffled, 'cols')
  check('重新编号：先列后行是一列一列从上到下', byColumns[0].x === 5 && byColumns[3].x === 5 && byColumns[3].y === 208.5 && byColumns[4].x === 105 && byColumns[4].y === 28.5)
  check('重新编号时同一行允许 1 mm 以内的错位', renumber([{ x: 105, y: 10.6, rotation: 0 }, { x: 5, y: 10, rotation: 0 }], 'rows')[0].x === 5)
  check('手动调整顺序：把第 3 个移到第 1 个', (() => {
    const moved = moveSlot(fresh.slots, 2, 0)
    return moved[0].y === 88.5 && moved[1].x === 5 && moved[1].y === 28.5 && moved.length === 8
  })())
  check('页数：8 格的版式 19 张从第 3 格开始是 3 页', pageCount(8, 19, 3) === 3 && pageCount(8, 8) === 1 && pageCount(8, 0) === 0)
  check('长度按 0.1 mm 取整', normalizeLayout({ ...fresh, slots: [{ x: 5.04, y: 10.06, rotation: 90 }], offset: { x: -0.26, y: 0 } }).slots[0].y === 10.1)
  const slotBoxes = [{ x: 3, y: 0, width: 60, height: 100 }, { x: 70, y: 5, width: 100, height: 60 }]
  alignElements(slotBoxes, 'top', a4)
  check('对齐也能用在格子所占的矩形上（arrange.ts 接受任意矩形）', slotBoxes[0].y === 0 && slotBoxes[1].y === 0)
}

/**
 * 源码规范：注释完整（tests/docs.ts）、文件不超过 500 行。
 */
function checkSource(): void {
  /**
   * frontend 目录。
   */
  const frontendRoot = fileURLToPath(new URL('..', import.meta.url))

  /**
   * 注释规范的问题清单。
   */
  const docProblems = checkDocs(frontendRoot)
  check(`模块级声明都有完整的 JSDoc（三行说明、@param、@returns）${docProblems.length > 0 ? `（${docProblems.length} 处）：\n  ` + docProblems.slice(0, 40).join('\n  ') : ''}`, docProblems.length === 0)

  /**
   * 超过 500 行的源码文件（AGENTS.md R11）。
   */
  const largeFiles = sourceFiles(join(frontendRoot, 'src')).filter((path) => readFileSync(path, 'utf8').split('\n').length > 500)
  check(`src 下的源码文件都不超过 500 行${largeFiles.length > 0 ? '：' + largeFiles.join('、') : ''}`, largeFiles.length === 0)
}

checkBindings()
checkElements()
checkHistory()
checkGeometry()
checkArrange()
checkClipboard()
checkBarcodes()
checkFieldsAndFormats()
checkTransform()
checkCompare()
checkSampleTemplate()
checkLayouts()
checkSource()

console.log(`前端检查全部通过：${passed} 项`)

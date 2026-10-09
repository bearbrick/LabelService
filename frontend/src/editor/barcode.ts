/**
 * 设计器里的条码预览：用 bwip-js 只做编码（得到条空宽度或二维矩阵），绘制交给画布。
 * 服务端用 ZXing.Net 生成，以服务端预览为准；这里的作用是让设计人员当场看到“能不能编码、放不放得下”。
 */
import bwipjs from 'bwip-js'

export interface LinearCode {
  kind: 'linear'
  /** 条、空交替的宽度（模块数），从条开始。 */
  widths: number[]
  /** 总模块数。 */
  modules: number
}

export interface MatrixCode {
  kind: 'matrix'
  /** 按行展开的模块，1 为黑。 */
  pixels: number[]
  columns: number
  rows: number
}

export type EncodeResult = { ok: true; code: LinearCode | MatrixCode } | { ok: false; message: string }

export const SYMBOLOGY_LABELS: Readonly<Record<string, string>> = {
  code128: 'Code 128',
  code39: 'Code 39',
  ean13: 'EAN-13',
  ean8: 'EAN-8',
  upca: 'UPC-A',
  itf14: 'ITF-14',
  'gs1-128': 'GS1-128',
  qr: 'QR Code',
  datamatrix: 'Data Matrix',
  pdf417: 'PDF417',
}

/** 模板里的码制写法对应 bwip-js 的编码器名。 */
const ENCODERS: Readonly<Record<string, string>> = {
  code128: 'code128',
  code39: 'code39',
  ean13: 'ean13',
  ean8: 'ean8',
  upca: 'upca',
  itf14: 'itf14',
  'gs1-128': 'gs1-128',
  qr: 'qrcode',
  datamatrix: 'datamatrix',
  pdf417: 'pdf417',
}

const MESSAGES: ReadonlyArray<[RegExp, string]> = [
  [/EAN-13 must be 12 or 13 digits/i, '必须是 12 或 13 位数字'],
  [/EAN-8 must be 7 or 8 digits/i, '必须是 7 或 8 位数字'],
  [/UPC-A must be 11 or 12 digits/i, '必须是 11 或 12 位数字'],
  [/ITF-14 must be 13 or 14 digits/i, '必须是 13 或 14 位数字'],
  [/must contain only digits/i, '只能包含数字'],
  [/check digit/i, '校验位不正确'],
  [/Code 39 must contain only/i, '只支持大写字母、数字和 - . 空格 $ / + %'],
  [/GS1|AI/, 'GS1 数据格式不正确，应写成 (01)06901234567892(10)A01 这样'],
]

const cache = new Map<string, EncodeResult>()

/** 编码条码内容，结果按“码制 + 内容 + 选项”缓存。 */
export function encode(symbology: string, text: string, ecLevel?: string): EncodeResult {
  const key = `${symbology}|${ecLevel ?? ''}|${text}`
  const hit = cache.get(key)
  if (hit) return hit
  const result = encodeUncached(symbology, text, ecLevel)
  if (cache.size > 500) cache.clear()
  cache.set(key, result)
  return result
}

function encodeUncached(symbology: string, text: string, ecLevel?: string): EncodeResult {
  const encoder = ENCODERS[symbology]
  if (!encoder) return { ok: false, message: `不支持的码制 ${symbology}` }
  try {
    // BWIPP 的选项写成 key=value 字符串，QR 的纠错级别是 eclevel。
    const options = symbology === 'qr' && ecLevel ? `eclevel=${ecLevel}` : ''
    const raw = bwipjs.raw(encoder, text, options)[0] as Partial<{ sbs: number[]; pixs: number[]; pixx: number; pixy: number }>
    if (raw.sbs) {
      return { ok: true, code: { kind: 'linear', widths: raw.sbs, modules: raw.sbs.reduce((sum, width) => sum + width, 0) } }
    }

    if (raw.pixs && raw.pixx && raw.pixy) {
      return { ok: true, code: { kind: 'matrix', pixels: raw.pixs, columns: raw.pixx, rows: raw.pixy } }
    }

    return { ok: false, message: '编码结果为空' }
  } catch (error) {
    const message = String(error instanceof Error ? error.message : error)
      .replace(/^bwipp\.[\w#]+:\s*/, '')
      .replace(/^bwip-js:\s*/, '')
    const translated = MESSAGES.find(([pattern]) => pattern.test(message))?.[1] ?? message
    return { ok: false, message: translated.length > 90 ? translated.slice(0, 90) + '…' : translated }
  }
}

/** 按框宽算一维码的模块宽度（打印点，向下取整），和服务端规则一致。 */
export function moduleDots(boxWidthMm: number, modules: number, dpi: number): number {
  return Math.floor((boxWidthMm * dpi) / 25.4 / modules + 1e-6)
}

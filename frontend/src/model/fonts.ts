/**
 * 模板里的字体名和浏览器字体的对应关系。默认字体是思源黑体（模板里写 SourceHanSansSC），另有思源宋体（SourceHanSerifSC）；
 * 客户在“素材与字体”页上传的字体按字体文件里的家族名使用（ADR 0011）。
 * 浏览器优先用服务端的同一份字体文件（/fonts/fonts.css，家族名“LabelService + 模板字体名”），
 * 服务端没有这个字体或还没下载完时退回本机安装的同款或相近字体，再不行按默认字体显示（和服务端的降级规则一致）。
 */

/**
 * 设计器可选的一种字体。
 */
export interface FontOption {
  /**
   * 模板 JSON 里写的 fontFamily。
   */
  value: string
  /**
   * 界面上显示的名字。
   */
  label: string
  /**
   * 浏览器里用的 CSS font-family。
   */
  css: string
  /**
   * 服务端有没有这个字体；没有时生成标签会降级到默认字体。
   */
  available: boolean
}

/**
 * 内置的一种字体：在设计器选项的基础上加上字体文件里可能出现的别名（和服务端 FontRegistry 的别名组一致）。
 */
export interface BuiltinFont extends Omit<FontOption, 'available'> {
  /**
   * 字体文件里可能的家族名，如 Noto Sans SC、Source Han Sans SC。
   */
  aliases: readonly string[]
}

/**
 * 内置的两种字体：模板名固定，CSS 里先写服务端字体，再写本机可能安装的同款或相近字体。
 */
export const BUILTIN_FONTS: readonly BuiltinFont[] = [
  {
    value: 'SourceHanSansSC',
    label: '思源黑体',
    css: '"LabelService SourceHanSansSC", "Source Han Sans SC", "Source Han Sans CN", "Noto Sans SC", "Noto Sans CJK SC", "Microsoft YaHei", sans-serif',
    aliases: ['Source Han Sans SC', 'Source Han Sans CN', 'Noto Sans SC', 'Noto Sans CJK SC', '思源黑体'],
  },
  {
    value: 'SourceHanSerifSC',
    label: '思源宋体',
    css: '"LabelService SourceHanSerifSC", "Source Han Serif SC", "Source Han Serif CN", "Noto Serif SC", "Noto Serif CJK SC", SimSun, serif',
    aliases: ['Source Han Serif SC', 'Source Han Serif CN', 'Noto Serif SC', 'Noto Serif CJK SC', '思源宋体'],
  },
]

/**
 * 字体名的比较形式：去掉空格、连字符、下划线，转小写（和服务端一致）。
 * @param name 字体名。
 * @returns 如 Noto Sans SC → notosanssc。
 */
function normalizeFontName(name: string): string {
  return name.replace(/[\s\-_]/g, '').toLowerCase()
}

/**
 * 字体文件里的家族名对应的模板字体名：属于内置字体的别名时用内置的模板名，否则原样。
 * @param familyName 字体文件里的家族名，如 Noto Sans SC。
 * @returns 如 SourceHanSansSC。
 */
export function templateFontName(familyName: string): string {
  const key = normalizeFontName(familyName)
  const builtin = BUILTIN_FONTS.find((font) => normalizeFontName(font.value) === key || font.aliases.some((alias) => normalizeFontName(alias) === key))
  return builtin?.value ?? familyName
}

/**
 * 默认字体（模板没写字体、或字体找不到时用）：思源黑体。
 */
export const DEFAULT_FONT: BuiltinFont = BUILTIN_FONTS[0]

/**
 * 服务端字体在浏览器里的家族名（fonts.css 里声明的名字）。
 * @param value 模板字体名。
 * @returns 如 LabelService SourceHanSansSC。
 */
export function serverFontFamily(value: string): string {
  return `LabelService ${value}`
}

/**
 * 模板字体名对应的 CSS font-family：内置字体用它的写法；其他字体（上传的）先用服务端字体和本机同名字体，
 * 都没有时按默认字体显示（和服务端的降级规则一致）。
 * @param name 模板里写的 fontFamily；空时是默认字体。
 * @returns CSS font-family。
 */
export function cssFontFamily(name: string | undefined): string {
  const builtin = BUILTIN_FONTS.find((font) => font.value === name)
  if (builtin) return builtin.css
  const trimmed = name?.trim()
  if (!trimmed) return DEFAULT_FONT.css
  // 引号和反斜杠会破坏 CSS 字符串，这种名字服务端也不会声明，直接按默认字体显示。
  if (/["\\]/.test(trimmed)) return DEFAULT_FONT.css
  return `"${serverFontFamily(trimmed)}", "${trimmed}", ${DEFAULT_FONT.css}`
}

/**
 * 设计器字体下拉框的选项：内置的两种在前（服务端没有时标“未安装”），再按服务端给的顺序列出上传的字体；
 * 模板里正在用、服务端却没有的字体也列出来（标“未安装”），下拉框才能显示出当前值。
 * @param serverFamilies 服务端已有的模板字体名（GET /fonts 的 templateFamilies）。
 * @param current 模板里正在用的字体名。
 * @returns 下拉框选项。
 */
export function fontOptions(serverFamilies: readonly string[], current?: string): FontOption[] {
  const options: FontOption[] = BUILTIN_FONTS.map((font) => ({ value: font.value, label: font.label, css: font.css, available: serverFamilies.includes(font.value) }))
  for (const family of serverFamilies) {
    if (!options.some((option) => option.value === family)) options.push({ value: family, label: family, css: cssFontFamily(family), available: true })
  }

  if (current && !options.some((option) => option.value === current)) options.push({ value: current, label: current, css: cssFontFamily(current), available: false })
  return options.map((option) => (option.available ? option : { ...option, label: `${option.label}（未安装）` }))
}

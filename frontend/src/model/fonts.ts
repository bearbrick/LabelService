/**
 * 模板里的字体名和浏览器字体的对应关系。服务端只打包思源黑体、思源宋体（SIL OFL），设计器也只提供这两种。
 * 浏览器优先用服务端提供的同一份字体文件（/fonts/fonts.css，家族名“LabelService + 模板字体名”），
 * 服务端没有字体或还没下载完时退回本机安装的同款或相近字体，这时文字宽度可能和服务端略有差异。
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
}

/**
 * 设计器可选的字体：思源黑体、思源宋体。CSS 里先写服务端字体，再写本机可能安装的同款或相近字体。
 */
export const FONT_OPTIONS: readonly FontOption[] = [
  {
    value: 'SourceHanSansSC',
    label: '思源黑体',
    css: '"LabelService SourceHanSansSC", "Source Han Sans SC", "Source Han Sans CN", "Noto Sans SC", "Noto Sans CJK SC", "Microsoft YaHei", sans-serif',
  },
  {
    value: 'SourceHanSerifSC',
    label: '思源宋体',
    css: '"LabelService SourceHanSerifSC", "Source Han Serif SC", "Source Han Serif CN", "Noto Serif SC", "Noto Serif CJK SC", SimSun, serif',
  },
]

/**
 * 模板字体名对应的 CSS font-family；不认识的名字按默认字体显示（和服务端的降级规则一致）。
 * @param name 模板里写的 fontFamily。
 * @returns CSS font-family。
 */
export function cssFontFamily(name: string | undefined): string {
  return (FONT_OPTIONS.find((font) => font.value === name) ?? FONT_OPTIONS[0]).css
}

/**
 * 服务端字体在浏览器里的家族名，提前加载用。
 */
export const SERVER_FONT_FAMILIES: readonly string[] = FONT_OPTIONS.map((font) => `LabelService ${font.value}`)

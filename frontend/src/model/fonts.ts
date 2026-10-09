/**
 * 模板里的字体名和浏览器字体的对应关系。服务端只打包思源黑体、思源宋体（SIL OFL），设计器也只提供这两种。
 * 服务端字体下载（T-016）完成前，浏览器用本机已安装的同款或相近字体显示，文字宽度可能和服务端略有差异。
 */

export interface FontOption {
  /** 模板 JSON 里写的 fontFamily。 */
  value: string
  /** 界面上显示的名字。 */
  label: string
  /** 浏览器里用的 CSS font-family。 */
  css: string
}

export const FONT_OPTIONS: readonly FontOption[] = [
  {
    value: 'SourceHanSansSC',
    label: '思源黑体',
    css: '"Source Han Sans SC", "Source Han Sans CN", "Noto Sans SC", "Noto Sans CJK SC", "Microsoft YaHei", sans-serif',
  },
  {
    value: 'SourceHanSerifSC',
    label: '思源宋体',
    css: '"Source Han Serif SC", "Source Han Serif CN", "Noto Serif SC", "Noto Serif CJK SC", SimSun, serif',
  },
]

/** 模板字体名对应的 CSS font-family；不认识的名字按默认字体显示（和服务端的降级规则一致）。 */
export function cssFontFamily(name: string | undefined): string {
  return (FONT_OPTIONS.find((font) => font.value === name) ?? FONT_OPTIONS[0]).css
}

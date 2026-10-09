/**
 * 图标元素用的图标库：GB/T 191 包装储运图示标志（简化示意，24×24 网格的线条路径）。
 * 模板里写 `icon: "gb191/fragile"`。服务端图标库（T-004）要提供同名图标。
 */

export interface LabelIcon {
  /** 中文名。 */
  name: string
  /** SVG 路径，24×24 网格，只描边不填充。 */
  d: string
}

export const LABEL_ICONS: Readonly<Record<string, LabelIcon>> = {
  'gb191/fragile': { name: '易碎物品', d: 'M7 3h10l-.7 5.6a4.3 4.3 0 0 1-8.6 0z M12 12.3v7.2 M8.5 20.5h7 M11 3.2l1.4 2.4-1.6 1.3 1.4 2' },
  'gb191/up': { name: '向上', d: 'M8 18V4.5 M4.8 7.7L8 4.5l3.2 3.2 M16 18V4.5 M12.8 7.7L16 4.5l3.2 3.2 M4 20.5h16' },
  'gb191/keep-dry': { name: '怕雨', d: 'M3.5 13a8.5 8.5 0 0 1 17 0z M12 13v5.5a2 2 0 0 1-4 0 M6 2.5l-.8 1.6 M11.2 1.5l-.8 1.6 M16.5 2.5l-.8 1.6' },
  'gb191/keep-from-sun': { name: '怕晒', d: 'M8.5 12a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0 M12 2.5V5 M12 19v2.5 M2.5 12H5 M19 12h2.5 M5.3 5.3l1.8 1.8 M16.9 16.9l1.8 1.8 M5.3 18.7l1.8-1.8 M16.9 7.1l1.8-1.8' },
  'gb191/no-hooks': { name: '禁用手钩', d: 'M12 3v9.5a3.5 3.5 0 1 1-3.5-3.5 M4 4l16 16 M20 4L4 20' },
  'gb191/temperature': { name: '温度极限', d: 'M10 14.6V5a2 2 0 0 1 4 0v9.6a4 4 0 1 1-4 0z M12 9v8 M16.5 6h3 M16.5 10h3' },
  'gb191/stack-limit': { name: '堆码层数极限', d: 'M4 20h16 M6 16h12v4H6z M6 12h12v4H6z M6 8h12v4H6z M12 2.5v3 M10.3 4.2L12 2.5l1.7 1.7' },
  'gb191/center-of-gravity': { name: '重心', d: 'M4 4h16v16H4z M12 8v8 M8 12h8 M9.5 12a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0' },
}

export const DEFAULT_ICON = 'gb191/fragile'

/** 按路径找图标；找不到时用默认图标。 */
export function findIcon(key: string | undefined): LabelIcon {
  return LABEL_ICONS[key ?? ''] ?? LABEL_ICONS[DEFAULT_ICON]
}

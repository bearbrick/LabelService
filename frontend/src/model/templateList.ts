/**
 * 模板多了以后的列表逻辑：模板树每个分组列哪些、搜索结果怎么排、最近打开、模板库分页。
 * 模板树只列一部分做导航，完整的列表在模板库里分页看。
 */

/**
 * 每个业务分组在模板树里最多列几个模板（最近修改的在前）。
 */
export const GROUP_LIMIT = 20

/**
 * 搜索时模板树里最多列几个结果。
 */
export const SEARCH_LIMIT = 30

/**
 * “最近打开”最多记几个。
 */
export const RECENT_LIMIT = 5

/**
 * 模板总数不超过这个数时分组默认展开；更多时默认收起，只展开当前模板所在的分组。
 */
export const EXPAND_ALL_LIMIT = 30

/**
 * 模板库每页几个。
 */
export const LIBRARY_PAGE_SIZE = 48

/**
 * 列表逻辑用到的模板属性。
 */
export interface ListTemplate {
  /**
   * 模板编码。
   */
  code: string
  /**
   * 模板名称。
   */
  name: string
  /**
   * 最后修改时间（ISO 8601）。
   */
  updatedAt: string
}

/**
 * 截取后的列表。
 */
export interface Limited<T> {
  /**
   * 要显示的项。
   */
  items: T[]
  /**
   * 符合条件的总数（可能比 items 多）。
   */
  total: number
}

/**
 * 分页结果。
 */
export interface Paged<T> {
  /**
   * 这一页的项。
   */
  items: T[]
  /**
   * 实际的页码（从 1 开始；要的页超出范围时换成最近的有效页）。
   */
  page: number
  /**
   * 总页数，至少 1。
   */
  pages: number
}

/**
 * 最近修改的在前；修改时间相同时按编码。
 * @param a 模板。
 * @param b 模板。
 * @returns 排序比较值。
 */
export function byRecentUpdate(a: ListTemplate, b: ListTemplate): number {
  return b.updatedAt.localeCompare(a.updatedAt) || a.code.localeCompare(b.code)
}

/**
 * 按分组归类，每组最近修改的在前。
 * @param templates 全部模板。
 * @param groupOf 模板归到哪个分组（不认识的分组由调用方归到 0“未分组”）。
 * @returns 分组 ID → 排好序的模板。
 */
export function groupTemplates<T extends ListTemplate>(templates: readonly T[], groupOf: (template: T) => number): Map<number, T[]> {
  const groups = new Map<number, T[]>()
  for (const template of templates) {
    const id = groupOf(template)
    const list = groups.get(id)
    if (list) list.push(template)
    else groups.set(id, [template])
  }

  for (const list of groups.values()) list.sort(byRecentUpdate)
  return groups
}

/**
 * 一个分组在模板树里显示的模板：前 limit 个；当前打开的模板排在后面时也加在最后，免得在树里找不到它。
 * @param sorted 分组里排好序的全部模板。
 * @param limit 最多几个。
 * @param current 当前打开的模板编码；没有时为空字符串。
 * @returns 要显示的模板和分组里的总数。
 */
export function visibleInGroup<T extends ListTemplate>(sorted: readonly T[], limit: number, current: string): Limited<T> {
  const items = sorted.slice(0, limit)
  const open = current ? sorted.slice(limit).find((template) => template.code === current) : undefined
  if (open) items.push(open)
  return { items, total: sorted.length }
}

/**
 * 搜索：名称或编码包含关键字（不区分大小写）。编码完全相同的排最前，其次编码或名称以关键字开头的，再按最近修改。
 * @param templates 全部模板。
 * @param query 关键字；去掉首尾空白后为空时没有结果。
 * @param limit 最多几个。
 * @returns 排好序的前 limit 个和匹配的总数。
 */
export function searchTemplates<T extends ListTemplate>(templates: readonly T[], query: string, limit: number): Limited<T> {
  const q = query.trim().toLowerCase()
  if (!q) return { items: [], total: 0 }
  // 0：编码完全相同；1：编码或名称以关键字开头；2：只是包含
  const rank = (template: T) => {
    const code = template.code.toLowerCase()
    if (code === q) return 0
    return code.startsWith(q) || template.name.toLowerCase().startsWith(q) ? 1 : 2
  }
  const matched = templates.filter((template) => template.code.toLowerCase().includes(q) || template.name.toLowerCase().includes(q))
  matched.sort((a, b) => rank(a) - rank(b) || byRecentUpdate(a, b))
  return { items: matched.slice(0, limit), total: matched.length }
}

/**
 * 记一次打开：放到最前面，去掉重复，最多 limit 个。
 * @param recent 原来的列表（新的在前）。
 * @param code 刚打开的模板编码。
 * @param limit 最多几个。
 * @returns 新的列表（不改原数组）。
 */
export function pushRecent(recent: readonly string[], code: string, limit: number): string[] {
  return [code, ...recent.filter((item) => item !== code)].slice(0, limit)
}

/**
 * 分页。
 * @param items 全部项。
 * @param page 要的页码（从 1 开始）。
 * @param size 每页几个。
 * @returns 这一页的项、实际页码和总页数。
 */
export function paginate<T>(items: readonly T[], page: number, size: number): Paged<T> {
  const pages = Math.max(1, Math.ceil(items.length / size))
  const actual = Math.min(Math.max(1, Math.floor(page) || 1), pages)
  return { items: items.slice((actual - 1) * size, actual * size), page: actual, pages }
}

/**
 * 分页条上显示的页码：第一页、最后一页和当前页前后各两页，中间断开的地方放省略号（0）。
 * @param page 当前页。
 * @param pages 总页数。
 * @returns 页码，0 表示省略号。
 */
export function pageButtons(page: number, pages: number): number[] {
  const shown = [...new Set([1, page - 2, page - 1, page, page + 1, page + 2, pages])].filter((p) => p >= 1 && p <= pages).sort((a, b) => a - b)
  const result: number[] = []
  for (const p of shown) {
    if (result.length > 0 && p - result[result.length - 1] > 1) result.push(0)
    result.push(p)
  }

  return result
}

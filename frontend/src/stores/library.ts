/**
 * 模板库页面的筛选关键字。放在页面外面，模板树的“在模板库里查看全部结果”可以把搜索词带过去。
 */
import { reactive } from 'vue'

/**
 * 模板库的筛选状态。
 */
export const library = reactive({
  /**
   * 按名称或编码筛选的关键字。
   */
  query: '',
})

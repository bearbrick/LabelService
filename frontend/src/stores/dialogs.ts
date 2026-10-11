/**
 * 全局的界面状态：确认框、一次性密钥框、新建模板对话框、模板树收起，以及复制到剪贴板。由 DialogHost、App 显示。
 */
import { reactive } from 'vue'
import { toast } from './designer.ts'

/**
 * 正在显示的确认框。
 */
interface ConfirmState {
  /**
   * 标题。
   */
  title: string
  /**
   * 说明文字。
   */
  text: string
  /**
   * 确定按钮的文字。
   */
  ok: string
  /**
   * 危险操作（确定按钮显示成红色）。
   */
  danger: boolean
  /**
   * 关闭时回传结果：确定为 true，取消为 false。
   */
  resolve: (value: boolean) => void
}

/**
 * 正在显示的一次性密钥框（新建调用方的 Key、重置的临时密码）。
 */
interface SecretState {
  /**
   * 标题。
   */
  title: string
  /**
   * 值的名称，如“API Key”。
   */
  label: string
  /**
   * 要展示的密钥或密码。
   */
  value: string
  /**
   * 提示，如“只显示这一次，请立即保存”。
   */
  hint: string
}

/**
 * 全局弹窗的状态。
 */
export const dialogs = reactive({
  /**
   * 确认框；没有显示时为 null。
   */
  confirm: null as ConfirmState | null,
  /**
   * 一次性密钥框；没有显示时为 null。
   */
  secret: null as SecretState | null,
  /**
   * 新建模板对话框。
   */
  newTemplate: false,
  /**
   * 模板工作区里收起左侧模板树。
   */
  navCollapsed: false,
})

/**
 * 弹出确认框，用户点确定时返回 true。
 * @param title 标题。
 * @param text 说明文字。
 * @param ok 确定按钮的文字。
 * @param danger 危险操作。
 * @returns 用户的选择：确定为 true；取消或被新的确认框顶替时为 false。
 */
export function confirmBox(title: string, text: string, ok = '确定', danger = false): Promise<boolean> {
  dialogs.confirm?.resolve(false)
  return new Promise((resolve) => {
    dialogs.confirm = { title, text, ok, danger, resolve }
  })
}

/**
 * 关闭确认框并回传结果。
 * @param value 确定为 true，取消为 false。
 */
export function closeConfirm(value: boolean): void {
  dialogs.confirm?.resolve(value)
  dialogs.confirm = null
}

/**
 * 显示只出现一次的密钥或临时密码。
 * @param title 标题。
 * @param label 值的名称。
 * @param value 密钥或密码。
 * @param hint 提示。
 */
export function showSecret(title: string, label: string, value: string, hint: string): void {
  dialogs.secret = { title, label, value, hint }
}

/**
 * 复制文本到系统剪贴板，并提示结果。
 * @param text 文本。
 * @returns 复制结束时完成。
 */
export async function copyText(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text)
    toast('已复制')
  } catch {
    toast('复制失败，请手动选中文本复制', true)
  }
}

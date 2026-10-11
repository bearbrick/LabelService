/**
 * 全局的界面状态：确认框、一次性密钥框、新建模板对话框、模板树收起，以及复制到剪贴板。由 DialogHost、App 显示。
 */
import { reactive } from 'vue'
import { toast } from './designer.ts'

interface ConfirmState {
  title: string
  text: string
  ok: string
  danger: boolean
  resolve: (value: boolean) => void
}

interface SecretState {
  title: string
  label: string
  value: string
  hint: string
}

export const dialogs = reactive({
  confirm: null as ConfirmState | null,
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
 */
export function confirmBox(title: string, text: string, ok = '确定', danger = false): Promise<boolean> {
  dialogs.confirm?.resolve(false)
  return new Promise((resolve) => {
    dialogs.confirm = { title, text, ok, danger, resolve }
  })
}

export function closeConfirm(value: boolean): void {
  dialogs.confirm?.resolve(value)
  dialogs.confirm = null
}

/**
 * 显示只出现一次的密钥或临时密码。
 */
export function showSecret(title: string, label: string, value: string, hint: string): void {
  dialogs.secret = { title, label, value, hint }
}

export async function copyText(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text)
    toast('已复制')
  } catch {
    toast('复制失败，请手动选中文本复制', true)
  }
}

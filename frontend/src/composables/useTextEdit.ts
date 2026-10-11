/**
 * 画布上直接编辑文本：双击文本元素后在原位置弹出输入框（字体、字号、对齐、旋转和画布一致），
 * 输入 {{ 时弹出字段补全；Ctrl+Enter 或点别处保存，Esc 取消。由 CanvasWorkspace 使用。
 */
import { computed, nextTick, reactive } from 'vue'
import type { Ref } from 'vue'
import type { DesignerStage } from '../editor/stage.ts'
import { defaultTextStyle } from '../model/elements.ts'
import { cssFontFamily } from '../model/fonts.ts'
import type { FieldDefinition } from '../model/template.ts'
import { MM_PER_PT } from '../model/units.ts'
import { commit, designer, findElement, select } from '../stores/designer.ts'

/**
 * 直接编辑文本的状态和操作。
 * @param getStage 取当前的画布（还没创建时为 null）。
 * @param editor 输入框元素。
 * @param wrap 画布的外层容器（编辑结束后把焦点还给它，快捷键才能继续用）。
 * @returns 编辑状态、补全列表的位置和各个事件处理函数。
 */
export function useTextEdit(getStage: () => DesignerStage | null, editor: Ref<HTMLTextAreaElement | undefined>, wrap: Ref<HTMLDivElement | undefined>) {
  /**
   * 编辑状态。
   */
  const edit = reactive({
    /**
     * 正在编辑的元素 ID；没在编辑时为 null。
     */
    id: null as string | null,
    /**
     * 输入框里的文本。
     */
    value: '',
    /**
     * 输入框的样式（位置、尺寸、字体，和画布上的文本一致）。
     */
    style: {} as Record<string, string>,
    /**
     * 字段补全的候选（最多 8 个）。
     */
    suggestions: [] as FieldDefinition[],
    /**
     * 补全列表里高亮的一项。
     */
    index: 0,
    /**
     * 要替换的 {{ 片段在文本里的起止位置。
     */
    match: null as { start: number; end: number } | null,
  })

  /**
   * 补全列表的位置：输入框正下方。
   */
  const suggestStyle = computed(() => ({ left: edit.style.left, top: `${parseFloat(edit.style.top ?? '0') + parseFloat(edit.style.height ?? '0') + 4}px` }))

  /**
   * 按元素当前的位置、缩放和样式摆放输入框（缩放、平移后也要调用）。
   */
  function positionEditor(): void {
    const stage = getStage()
    const element = edit.id ? findElement(edit.id) : undefined
    if (!element || element.type !== 'text' || !stage) return
    const style = { ...defaultTextStyle(), ...element.style }
    const scale = stage.scale
    const fontSize = style.fontSize * MM_PER_PT * scale
    const point = stage.toScreen(element.x, element.y)
    edit.style = {
      left: `${point.x}px`,
      top: `${point.y}px`,
      width: `${Math.max(element.width * scale, 60)}px`,
      height: `${Math.max(element.height * scale, fontSize * 1.5)}px`,
      fontSize: `${fontSize}px`,
      fontFamily: cssFontFamily(style.fontFamily),
      fontWeight: style.bold ? '700' : '400',
      fontStyle: style.italic ? 'italic' : 'normal',
      lineHeight: String(style.lineHeight),
      textAlign: style.align,
      letterSpacing: `${(style.letterSpacing ?? 0) * scale}px`,
      transform: `rotate(${element.rotation ?? 0}deg)`,
    }
  }

  /**
   * 开始编辑一个文本元素（锁定的不能编辑）：选中它，画布上隐藏它，显示输入框并全选文字。
   * @param id 元素 ID。
   */
  function startEdit(id: string): void {
    const stage = getStage()
    const element = findElement(id)
    if (!element || element.type !== 'text' || element.locked || !stage) return
    select([id])
    edit.id = id
    edit.value = element.content
    edit.suggestions = []
    positionEditor()
    stage.setEditing(id)
    nextTick(() => {
      editor.value?.focus()
      editor.value?.select()
    })
  }

  /**
   * 结束编辑：保存时文本有变化才记录撤销；画布上重新显示元素，焦点还给画布。
   * @param save 是否保存。
   */
  function finishEdit(save: boolean): void {
    if (!edit.id) return
    const element = findElement(edit.id)
    edit.id = null
    edit.suggestions = []
    if (element?.type === 'text' && save && edit.value !== element.content) {
      element.content = edit.value
      commit()
    }

    getStage()?.setEditing(null)
    wrap.value?.focus({ preventScroll: true })
  }

  /**
   * 输入时检查光标前是不是 {{ 加字段名的开头，是就按 key 或名称找候选字段。
   */
  function onEditInput(): void {
    const textarea = editor.value
    if (!textarea) return
    const before = textarea.value.slice(0, textarea.selectionStart)
    const match = /\{\{\s*([A-Za-z_]\w*)?$/.exec(before)
    if (!match) {
      edit.suggestions = []
      return
    }

    const query = (match[1] ?? '').toLowerCase()
    edit.suggestions = designer.template.fields
      .filter((field) => field.key.toLowerCase().startsWith(query) || (field.name ?? '').includes(match[1] ?? ''))
      .slice(0, 8)
    edit.index = 0
    edit.match = { start: match.index, end: textarea.selectionStart }
  }

  /**
   * 选中一个补全：把 {{ 片段换成完整的 {{key}}，光标放到后面。
   * @param field 字段。
   */
  function pickSuggestion(field: FieldDefinition): void {
    const match = edit.match
    if (!match) return
    const insert = `{{${field.key}}}`
    edit.value = edit.value.slice(0, match.start) + insert + edit.value.slice(match.end)
    edit.suggestions = []
    nextTick(() => {
      const position = match.start + insert.length
      editor.value?.focus()
      editor.value?.setSelectionRange(position, position)
    })
  }

  /**
   * 输入框的按键（不传给设计器的快捷键）：有补全时上下键选择、回车或 Tab 选中、Esc 关闭补全；
   * 否则 Esc 取消编辑，Ctrl+Enter 保存。
   * @param event 键盘事件。
   */
  function onEditKey(event: KeyboardEvent): void {
    event.stopPropagation()
    if (edit.suggestions.length) {
      const count = edit.suggestions.length
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault()
        edit.index = (edit.index + (event.key === 'ArrowDown' ? 1 : count - 1)) % count
        return
      }

      if (event.key === 'Enter' || event.key === 'Tab') {
        event.preventDefault()
        pickSuggestion(edit.suggestions[edit.index])
        return
      }

      if (event.key === 'Escape') {
        event.preventDefault()
        edit.suggestions = []
        return
      }
    }

    if (event.key === 'Escape') {
      event.preventDefault()
      finishEdit(false)
    } else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      finishEdit(true)
    }
  }

  /**
   * 输入框失去焦点时保存（等这一轮事件处理完再判断，点补全项不算离开）。
   */
  function onEditBlur(): void {
    setTimeout(() => {
      if (edit.id && document.activeElement !== editor.value) finishEdit(true)
    }, 0)
  }

  return { edit, suggestStyle, positionEditor, startEdit, finishEdit, onEditInput, pickSuggestion, onEditKey, onEditBlur }
}

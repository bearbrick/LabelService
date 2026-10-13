<script setup lang="ts">
/**
 * 数据源页的接口资料抽屉，不占字段表空间；支持关闭、Esc、焦点循环及返回入口按钮。
 */
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { copyText } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'

/**
 * 抽屉展示参数。
 */
const props = defineProps<{
  /**
   * 当前打开的资料。
   */
  kind: 'sample' | 'schema'
  /**
   * 模板编码。
   */
  code: string
  /**
   * 当前草稿生成的调用样例。
   */
  sample: string
}>()

/**
 * 抽屉关闭事件。
 */
const emit = defineEmits<{
  /**
   * 关闭后返回原入口。
   */
  close: []
}>()

/**
 * 抽屉元素，用于限制键盘焦点。
 */
const panel = ref<HTMLElement | null>(null)

/**
 * 打开抽屉前的入口按钮。
 */
const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null

/**
 * 截获 Esc 和 Tab，避免设计器快捷键处理关闭事件或焦点移出抽屉。
 * @param event 键盘事件。
 */
function onKey(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    emit('close')
  } else if (event.key === 'Tab') {
    const buttons = panel.value?.querySelectorAll<HTMLElement>('button:not(:disabled)')
    if (!buttons?.length) return
    const first = buttons[0]!
    const last = buttons[buttons.length - 1]!
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
  }
}

onMounted(() => {
  panel.value?.querySelector<HTMLElement>('button')?.focus()
  window.addEventListener('keydown', onKey, true)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey, true)
  previousFocus?.focus()
})
</script>

<template>
  <Teleport to="body">
    <div class="drawer-back" @click="emit('close')"></div>
    <aside ref="panel" class="drawer fields-api-drawer" role="dialog" aria-modal="true" :aria-label="props.kind === 'sample' ? '调用样例' : '字段定义接口'">
      <div class="modal-h">
        <h3>{{ props.kind === 'sample' ? '调用样例' : '字段定义接口' }}</h3>
        <button class="icon-btn" type="button" aria-label="关闭" @click="emit('close')"><AppIcon name="x" /></button>
      </div>
      <div class="modal-b">
        <template v-if="props.kind === 'sample'">
          <p class="hint">根据当前草稿生成，只列接口需要提供的字段，模板提供的值不需要传。</p>
          <div class="endpoint"><span class="method">POST</span>/api/v1/render</div>
          <pre class="code">{{ props.sample }}</pre>
        </template>
        <template v-else>
          <div class="endpoint"><span class="method">GET</span>/api/v1/templates/{{ props.code }}/schema</div>
          <p class="hint">返回已发布版本的字段定义、数据来源和调用样例。草稿改动发布后才对接口生效。</p>
          <p class="hint">source 为 input 时调用方传值；template 时使用模板配置；省略时保持旧版接口优先的回退规则。</p>
        </template>
      </div>
      <div class="modal-f">
        <button class="btn" type="button" @click="copyText(props.kind === 'sample' ? props.sample : `/api/v1/templates/${props.code}/schema`)"><AppIcon name="copy" small />{{ props.kind === 'sample' ? '复制样例' : '复制接口地址' }}</button>
        <button class="btn" type="button" @click="emit('close')">关闭</button>
      </div>
    </aside>
  </Teleport>
</template>

<style scoped>
.fields-api-drawer { width: min(600px, 100vw); }
.fields-api-drawer .modal-b { overflow: auto; }
</style>

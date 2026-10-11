<script setup lang="ts">
/**
 * 面包屑前的“收起 / 展开模板树”按钮，用在模板工作区和拼版设计器。
 * 窗口较窄时打开设计器会自动收起模板树，给画布留出宽度；用户之后手动展开不会再被收起，除非窗口又从宽变窄。
 */
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { dialogs } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'

/**
 * 窗口比这个宽度窄时自动收起模板树，px。
 * 设计器左右两栏加边距约 600px，模板树 280px，窗口 1440px 时画布还有约 560px。
 */
const AUTO_COLLAPSE_WIDTH = 1440

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 当前视图是否需要画布宽度（设计器、拼版设计器）；为 true 时窗口较窄就自动收起。
   */
  auto: boolean
}>()

/**
 * 上次检查时窗口是否够宽，用来判断“从宽变窄”的那一刻。
 */
let wide = window.innerWidth >= AUTO_COLLAPSE_WIDTH

/**
 * 当前视图需要画布宽度且窗口较窄时收起模板树。
 */
function collapseIfNarrow(): void {
  if (props.auto && window.innerWidth < AUTO_COLLAPSE_WIDTH) dialogs.navCollapsed = true
}

/**
 * 窗口大小变化：只在从宽变窄跨过界线时收起一次，不覆盖用户在窄窗口里手动展开的选择。
 */
function onResize(): void {
  const now = window.innerWidth >= AUTO_COLLAPSE_WIDTH
  if (wide && !now) collapseIfNarrow()
  wide = now
}

// 从数据源、接口调试等页签切回设计器时也检查一次
watch(
  () => props.auto,
  (auto) => {
    if (auto) collapseIfNarrow()
  },
)

onMounted(() => {
  collapseIfNarrow()
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => window.removeEventListener('resize', onResize))
</script>

<template>
  <button
    type="button"
    class="icon-btn xs"
    :aria-label="dialogs.navCollapsed ? '展开模板树' : '收起模板树'"
    :data-tip="dialogs.navCollapsed ? '展开模板树' : '收起模板树'"
    @click="dialogs.navCollapsed = !dialogs.navCollapsed"
  >
    <AppIcon :name="dialogs.navCollapsed ? 'chevR' : 'chevL'" small />
  </button>
</template>

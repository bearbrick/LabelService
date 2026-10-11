<script setup lang="ts">
/**
 * 图层：上面的在上层。单击选择（Shift 多选），拖动调整叠放顺序，双击重命名。
 */
import { computed, nextTick, ref } from 'vue'
import { ELEMENT_META } from '../model/elements.ts'
import type { LabelElement } from '../model/template.ts'
import { commit, designer, moveLayer, select, toast, toggleHidden, toggleLock } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'

/**
 * 图层列表：上层的在前（模板里后面的元素在上层）。
 */
const topFirst = computed(() => [...designer.template.elements].reverse())

/**
 * 正在拖动的元素 ID。
 */
const dragging = ref<string | null>(null)

/**
 * 拖动经过的元素 ID（显示放置位置）。
 */
const over = ref<string | null>(null)

/**
 * 正在重命名的元素 ID。
 */
const renaming = ref<string | null>(null)

/**
 * 单击选择（Shift、Ctrl 加选或减选）；隐藏的元素不能选中。
 * @param element 元素。
 * @param event 鼠标事件。
 */
function choose(element: LabelElement, event: MouseEvent): void {
  if (element.hidden) {
    toast('隐藏的元素不能在画布上选中，先点眼睛图标显示')
    return
  }

  if (event.shiftKey || event.ctrlKey || event.metaKey) {
    select(designer.selection.includes(element.id) ? designer.selection.filter((id) => id !== element.id) : [...designer.selection, element.id])
  } else {
    select([element.id])
  }
}

/**
 * 开始重命名：显示输入框并全选文字。
 * @param element 元素。
 */
function startRename(element: LabelElement): void {
  renaming.value = element.id
  nextTick(() => {
    const input = document.getElementById(`rename-${element.id}`) as HTMLInputElement | null
    input?.focus()
    input?.select()
  })
}

/**
 * 结束重命名并记录撤销。
 */
function endRename(): void {
  renaming.value = null
  commit()
}

/**
 * 开始拖动图层。
 * @param event 拖动事件。
 * @param element 元素。
 */
function onDragStart(event: DragEvent, element: LabelElement): void {
  dragging.value = element.id
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

/**
 * 放下：把拖动的元素放到目标上面一层。
 * @param target 目标元素。
 */
function onDrop(target: LabelElement): void {
  const id = dragging.value
  dragging.value = null
  over.value = null
  if (id && id !== target.id) moveLayer(id, target.id)
}
</script>

<template>
  <div class="pbody" style="padding: 8px">
    <div v-if="!designer.template.elements.length" class="empty"><AppIcon name="layers" />画布上还没有元素</div>
    <div
      v-for="element in topFirst"
      :key="element.id"
      class="layer"
      :class="{ on: designer.selection.includes(element.id), off: element.hidden, 'drop-above': over === element.id }"
      draggable="true"
      @dragstart="onDragStart($event, element)"
      @dragover.prevent="over = element.id"
      @drop.prevent="onDrop(element)"
      @dragend="over = null"
      @click="choose(element, $event)"
      @dblclick="startRename(element)"
    >
      <AppIcon :name="ELEMENT_META[element.type].icon" small />
      <input
        v-if="renaming === element.id"
        :id="`rename-${element.id}`"
        v-model="element.name"
        class="inp"
        aria-label="图层名称"
        @blur="endRename()"
        @keydown.enter="($event.target as HTMLInputElement).blur()"
        @click.stop
      />
      <span v-else class="nm">{{ element.name }}</span>
      <button class="icon-btn xs" type="button" :aria-label="element.hidden ? '显示' : '隐藏'" @click.stop="toggleHidden(element)">
        <AppIcon :name="element.hidden ? 'eyeOff' : 'eye'" small />
      </button>
      <button class="icon-btn xs" type="button" :class="{ on: element.locked }" :aria-label="element.locked ? '解锁' : '锁定'" @click.stop="toggleLock(element)">
        <AppIcon :name="element.locked ? 'lock' : 'unlock'" small />
      </button>
    </div>
    <p v-if="designer.template.elements.length" class="hint" style="padding: 8px">拖动行调整叠放顺序，上面的在上层。双击重命名。</p>
  </div>
</template>

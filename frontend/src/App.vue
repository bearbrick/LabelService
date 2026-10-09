<script setup lang="ts">
/**
 * 设计器骨架：左侧元素库和字段，中间画布，右侧属性。
 * 目前加载示例模板，可以选中、拖动元素、改坐标；保存、新增元素等见 docs/tasks/T-013、T-014。
 */
import { computed, reactive, ref } from 'vue'
import DesignerCanvas from './components/DesignerCanvas.vue'
import sample from './samples/box-label.json'
import { ELEMENT_LABELS, ELEMENT_TYPES } from './model/template'
import type { LabelTemplate } from './model/template'
import { roundMm } from './model/units'

const template = reactive(structuredClone(sample) as unknown as LabelTemplate)
const selectedId = ref<string | null>(null)
const scale = ref(6)

const selected = computed(() => template.elements.find((element) => element.id === selectedId.value) ?? null)

function move(id: string, x: number, y: number) {
  const element = template.elements.find((item) => item.id === id)
  if (element) {
    element.x = x
    element.y = y
  }
}

function setNumber(key: 'x' | 'y' | 'width' | 'height' | 'rotation', value: string) {
  const number = Number(value)
  if (selected.value && Number.isFinite(number)) selected.value[key] = key === 'rotation' ? number : roundMm(number)
}
</script>

<template>
  <div class="shell">
    <header class="topbar">
      <strong>标签设计器</strong>
      <span class="muted">{{ template.name }} · {{ template.code }} · {{ template.page.width }}×{{ template.page.height }} mm</span>
      <span class="spacer" />
      <label class="muted">缩放
        <input v-model.number="scale" type="range" min="2" max="16" step="1" />
        {{ Math.round((scale / 6) * 100) }}%
      </label>
    </header>

    <aside class="panel left">
      <h3>元素库</h3>
      <ul class="palette">
        <li v-for="type in ELEMENT_TYPES" :key="type" title="拖入画布（T-013）">{{ ELEMENT_LABELS[type] }}</li>
      </ul>
      <h3>数据字段</h3>
      <ul class="fields">
        <li v-for="field in template.fields" :key="field.key">
          <code>{{ field.key }}</code><span class="muted">{{ field.name }} · {{ field.type }}</span>
        </li>
      </ul>
    </aside>

    <main class="canvas">
      <DesignerCanvas :template="template" :scale="scale" :selected-id="selectedId" @select="selectedId = $event" @move="move" />
    </main>

    <aside class="panel right">
      <h3>属性</h3>
      <template v-if="selected">
        <p class="muted">{{ ELEMENT_LABELS[selected.type] }} · {{ selected.id }}</p>
        <div class="grid">
          <label v-for="key in (['x', 'y', 'width', 'height', 'rotation'] as const)" :key="key">
            {{ { x: 'X', y: 'Y', width: '宽', height: '高', rotation: '旋转' }[key] }}
            <input :value="selected[key] ?? 0" type="number" step="0.1" @change="setNumber(key, ($event.target as HTMLInputElement).value)" />
          </label>
        </div>
      </template>
      <p v-else class="muted">未选中元素。模板 {{ template.page.width }}×{{ template.page.height }} mm，{{ template.page.dpi ?? 300 }} dpi。</p>
    </aside>
  </div>
</template>

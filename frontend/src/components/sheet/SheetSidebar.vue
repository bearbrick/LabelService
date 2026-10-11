<script setup lang="ts">
/**
 * 拼版设计器左侧：尺寸相同的已发布模板（拖到纸上生成格子，点一下换格子里的预览），自动排列（行列、页边距、间距，一键排满）。
 */
import { computed, reactive, watch } from 'vue'
import { fitGrid } from '../../model/layoutArrange.ts'
import type { FillOrder } from '../../model/layout.ts'
import { applyArrange, matchingTemplates, setPreview, sheet } from '../../stores/sheet.ts'
import TemplateThumb from '../admin/TemplateThumb.vue'

defineProps<{
  /**
   * 只读：不能拖入模板、不能自动排列。
   */
  readOnly: boolean
}>()

/**
 * 自动排列的参数（打开版式时按纸张排满）。
 */
const form = reactive({
  /**
   * 行数。
   */
  rows: 1,
  /**
   * 列数。
   */
  cols: 1,
  /**
   * 上边距，mm。
   */
  marginTop: 0,
  /**
   * 左边距，mm。
   */
  marginLeft: 0,
  /**
   * 列间距，mm。
   */
  gapX: 0,
  /**
   * 行间距，mm。
   */
  gapY: 0,
  /**
   * 格子都转 90°。
   */
  rotate: false,
  /**
   * 填充顺序。
   */
  order: 'rows' as FillOrder,
})
/**
 * 按当前参数排列是否放得下（放不下时提示，按钮不可用）。
 */
const fits = computed(() => {
  const layout = sheet.layout
  if (!layout) return true
  const width = form.rotate ? layout.slot.height : layout.slot.width
  const height = form.rotate ? layout.slot.width : layout.slot.height
  return form.marginLeft + form.cols * width + (form.cols - 1) * form.gapX <= layout.paper.width + 0.05
    && form.marginTop + form.rows * height + (form.rows - 1) * form.gapY <= layout.paper.height + 0.05
})

/**
 * 按纸张和格子尺寸算出最多几行几列、居中的页边距。
 */
function maximize(): void {
  const layout = sheet.layout
  if (!layout) return
  Object.assign(form, fitGrid(layout.paper, layout.slot, form.rotate, form.gapX, form.gapY))
}

watch(() => sheet.layout?.code, maximize, { immediate: true })

/**
 * 开始拖模板：记下模板编码，同时把格子里的预览换成它。
 * @param event 拖动事件。
 * @param code 模板编码。
 */
function onDragStart(event: DragEvent, code: string): void {
  if (!event.dataTransfer) return
  event.dataTransfer.effectAllowed = 'copy'
  event.dataTransfer.setData('text/plain', code)
  void setPreview(code)
}
</script>

<template>
  <aside class="panel sheet-left" aria-label="模板与自动排列">
    <div class="props">
      <h4>同尺寸的模板</h4>
      <p class="hint" style="margin-top: 0">只列出 {{ sheet.layout?.slot.width }} × {{ sheet.layout?.slot.height }} mm 的已发布模板。拖到纸上生成格子，点一下在格子里预览它。</p>
      <div v-if="!matchingTemplates.length" class="hint">没有尺寸相同的已发布模板，格子里只显示外框。</div>
      <div
        v-for="item in matchingTemplates"
        :key="item.code"
        class="sheet-tpl"
        :class="{ on: sheet.previewCode === item.code }"
        :draggable="!readOnly"
        :title="readOnly ? item.name : `拖到纸上生成格子；点击在格子里预览“${item.name}”`"
        @dragstart="onDragStart($event, item.code)"
        @click="setPreview(item.code)"
      >
        <TemplateThumb :code="item.code" :updated-at="item.updatedAt" :alt="item.name" />
        <span class="n">{{ item.name }}<small class="mono">{{ item.code }} · v{{ item.latestVersion }}</small></span>
      </div>
    </div>
    <div class="pdiv"></div>
    <fieldset class="props fs" :disabled="readOnly">
      <h4>自动排列</h4>
      <div class="pgrid">
        <div class="cell wide"><span>行</span><input v-model.number="form.rows" class="inp num" type="number" min="1" max="50" aria-label="行数" /></div>
        <div class="cell wide"><span>列</span><input v-model.number="form.cols" class="inp num" type="number" min="1" max="50" aria-label="列数" /></div>
        <div class="cell wide"><span>上边</span><div class="unit" data-unit="mm"><input v-model.number="form.marginTop" class="inp num" type="number" step="0.5" min="0" aria-label="上页边距" /></div></div>
        <div class="cell wide"><span>左边</span><div class="unit" data-unit="mm"><input v-model.number="form.marginLeft" class="inp num" type="number" step="0.5" min="0" aria-label="左页边距" /></div></div>
        <div class="cell wide"><span>横距</span><div class="unit" data-unit="mm"><input v-model.number="form.gapX" class="inp num" type="number" step="0.5" min="0" aria-label="水平间距" /></div></div>
        <div class="cell wide"><span>纵距</span><div class="unit" data-unit="mm"><input v-model.number="form.gapY" class="inp num" type="number" step="0.5" min="0" aria-label="垂直间距" /></div></div>
      </div>
      <label class="check"><input v-model="form.rotate" type="checkbox" @change="maximize" />格子转 90°（竖着放）</label>
      <div class="prow" style="margin-top: 8px">
        <label for="sh-order">顺序</label>
        <select id="sh-order" v-model="form.order" class="sel"><option value="rows">先行后列</option><option value="cols">先列后行</option></select>
      </div>
      <p v-if="!fits" class="form-err" style="margin: 0 0 8px">按这些参数排会超出纸张。</p>
      <div class="btn-row">
        <button class="btn" type="button" @click="maximize">按纸张算最多</button>
        <button class="btn primary" type="button" :disabled="form.rows < 1 || form.cols < 1" @click="applyArrange(form)">一键排满 {{ form.rows * form.cols }} 格</button>
      </div>
      <p class="hint">排满会替换现有的格子，可以撤销；之后再拖动微调。</p>
    </fieldset>
  </aside>
</template>

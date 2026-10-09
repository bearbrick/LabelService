<script setup lang="ts">
/**
 * 右侧属性面板：没选中时显示模板属性，多选时显示对齐和分布，单选时显示元素属性。
 */
import { computed } from 'vue'
import { ELEMENT_META } from '../model/elements.ts'
import { CANVAS_PRESETS } from '../model/presets.ts'
import { roundMm } from '../model/units.ts'
import {
  align,
  allSelectedLocked,
  commit,
  designer,
  distribute,
  duplicate,
  removeSelected,
  report,
  selectedElement,
  selectedElements,
  setPage,
  setRotation,
  toggleHidden,
  toggleLock,
  toggleLockSelection,
} from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'
import CodeProps from './props/CodeProps.vue'
import MediaProps from './props/MediaProps.vue'
import ShapeProps from './props/ShapeProps.vue'
import TextProps from './props/TextProps.vue'

const page = computed(() => designer.template.page)
const metric = computed(() => (selectedElement.value ? designer.metrics[selectedElement.value.id] : undefined))
const json = computed(() => (selectedElement.value ? JSON.stringify(selectedElement.value, null, 2) : ''))

function onGeometry(): void {
  const element = selectedElement.value
  if (!element) return
  element.x = roundMm(element.x)
  element.y = roundMm(element.y)
  element.width = Math.max(0, roundMm(element.width))
  element.height = Math.max(0, roundMm(element.height))
  commit()
}

function onPage(): void {
  setPage(Math.max(5, page.value.width || 0), Math.max(5, page.value.height || 0))
}
</script>

<template>
  <aside class="panel rightpanel" aria-label="属性">
    <!-- 模板属性 -->
    <div v-if="!selectedElements.length" class="props">
      <h4><AppIcon name="tag" small />模板属性</h4>
      <div class="prow"><label for="tpl-name">名称</label><input id="tpl-name" v-model="designer.template.name" class="inp" @change="commit()" /></div>
      <div class="prow"><label for="tpl-code">编码</label><input id="tpl-code" v-model.trim="designer.template.code" class="inp mono" placeholder="如 BOX_LABEL" @change="commit()" /></div>
      <div class="pdiv"></div>
      <h4>画布</h4>
      <div class="pgrid">
        <div class="cell"><span>宽</span><div class="unit" data-unit="mm"><input v-model.number="page.width" class="inp num" type="number" step="0.5" min="5" max="1000" aria-label="画布宽度" @change="onPage()" /></div></div>
        <div class="cell"><span>高</span><div class="unit" data-unit="mm"><input v-model.number="page.height" class="inp num" type="number" step="0.5" min="5" max="1000" aria-label="画布高度" @change="onPage()" /></div></div>
      </div>
      <div class="prow">
        <label for="tpl-dpi">DPI</label>
        <select id="tpl-dpi" v-model.number="page.dpi" class="sel" @change="commit()">
          <option :value="203">203（8 点/mm）</option>
          <option :value="300">300（11.8 点/mm）</option>
          <option :value="600">600（23.6 点/mm）</option>
        </select>
      </div>
      <div class="sect-h" style="margin-top: 14px"><span>画布预设</span></div>
      <div class="chips">
        <button
          v-for="preset in CANVAS_PRESETS"
          :key="preset.name"
          class="chip"
          type="button"
          :class="{ on: preset.width === page.width && preset.height === page.height }"
          @click="setPage(preset.width, preset.height)"
        >
          {{ preset.name }}
        </button>
      </div>
      <div class="pdiv"></div>
      <h4>设计辅助</h4>
      <div class="prow">
        <label for="tpl-grid">网格间距</label>
        <select id="tpl-grid" v-model.number="designer.ui.gridSize" class="sel">
          <option :value="0.5">0.5 mm</option>
          <option :value="1">1 mm</option>
          <option :value="2">2 mm</option>
          <option :value="5">5 mm</option>
        </select>
      </div>
      <p class="hint">按住空格拖动画布，Ctrl+滚轮缩放，方向键微调 0.1mm。状态栏的“快捷键”里有完整列表。</p>
      <template v-if="report.unusedFields.length">
        <div class="pdiv"></div>
        <p class="hint">没有元素引用的字段：<span class="mono">{{ report.unusedFields.join('、') }}</span></p>
      </template>
    </div>

    <!-- 多选 -->
    <div v-else-if="selectedElements.length > 1" class="props">
      <h4><AppIcon name="layers" small />已选 {{ selectedElements.length }} 个元素</h4>
      <div class="sect-h"><span>对齐（以选区边界为准）</span></div>
      <div class="btn-row">
        <button class="icon-btn" type="button" aria-label="左对齐" @click="align('left')"><AppIcon name="alignL" /></button>
        <button class="icon-btn" type="button" aria-label="水平居中" @click="align('hcenter')"><AppIcon name="alignCH" /></button>
        <button class="icon-btn" type="button" aria-label="右对齐" @click="align('right')"><AppIcon name="alignR" /></button>
        <button class="icon-btn" type="button" aria-label="顶对齐" @click="align('top')"><AppIcon name="alignT" /></button>
        <button class="icon-btn" type="button" aria-label="垂直居中" @click="align('vcenter')"><AppIcon name="alignCV" /></button>
        <button class="icon-btn" type="button" aria-label="底对齐" @click="align('bottom')"><AppIcon name="alignB" /></button>
      </div>
      <div class="sect-h" style="margin-top: 12px"><span>分布</span></div>
      <div class="btn-row">
        <button class="btn sm" type="button" :disabled="selectedElements.length < 3" @click="distribute('h')"><AppIcon name="distH" small />水平等距</button>
        <button class="btn sm" type="button" :disabled="selectedElements.length < 3" @click="distribute('v')"><AppIcon name="distV" small />垂直等距</button>
      </div>
      <div class="pdiv"></div>
      <div class="btn-row">
        <button class="btn sm" type="button" @click="duplicate()">复制一份</button>
        <button class="btn sm" type="button" @click="toggleLockSelection()">{{ allSelectedLocked ? '解锁' : '锁定' }}</button>
        <button class="btn sm danger" type="button" @click="removeSelected()">删除</button>
      </div>
    </div>

    <!-- 单个元素 -->
    <div v-else-if="selectedElement" class="props">
      <h4>
        <AppIcon :name="ELEMENT_META[selectedElement.type].icon" small />
        <input v-model="selectedElement.name" class="inp" style="height: 26px" aria-label="元素名称" @change="commit()" />
      </h4>
      <div class="pgrid">
        <div class="cell"><span>X</span><div class="unit" data-unit="mm"><input v-model.number="selectedElement.x" class="inp num" type="number" step="0.1" aria-label="X" @change="onGeometry()" /></div></div>
        <div class="cell"><span>Y</span><div class="unit" data-unit="mm"><input v-model.number="selectedElement.y" class="inp num" type="number" step="0.1" aria-label="Y" @change="onGeometry()" /></div></div>
        <div class="cell">
          <span>宽</span>
          <div class="unit" data-unit="mm"><input v-model.number="selectedElement.width" class="inp num" type="number" step="0.1" min="0" aria-label="宽" :disabled="selectedElement.type === 'line' && selectedElement.width === 0" @change="onGeometry()" /></div>
        </div>
        <div class="cell">
          <span>高</span>
          <div class="unit" data-unit="mm"><input v-model.number="selectedElement.height" class="inp num" type="number" step="0.1" min="0" aria-label="高" :disabled="selectedElement.type === 'line' && selectedElement.height === 0" @change="onGeometry()" /></div>
        </div>
      </div>
      <div class="pgrid">
        <div class="cell">
          <span aria-hidden="true">↻</span>
          <div class="unit" data-unit="°">
            <input
              class="inp num"
              type="number"
              step="1"
              aria-label="旋转角度"
              :value="selectedElement.rotation ?? 0"
              :disabled="selectedElement.type === 'line'"
              @change="setRotation(selectedElement, Number(($event.target as HTMLInputElement).value))"
            />
          </div>
        </div>
        <div class="btn-row" style="justify-content: flex-end">
          <button class="icon-btn" type="button" :class="{ on: selectedElement.locked }" :aria-label="selectedElement.locked ? '解锁' : '锁定'" @click="toggleLock(selectedElement)">
            <AppIcon :name="selectedElement.locked ? 'lock' : 'unlock'" />
          </button>
          <button class="icon-btn" type="button" :aria-label="selectedElement.hidden ? '显示' : '隐藏'" @click="toggleHidden(selectedElement)">
            <AppIcon :name="selectedElement.hidden ? 'eyeOff' : 'eye'" />
          </button>
          <button class="icon-btn" type="button" aria-label="删除" @click="removeSelected()"><AppIcon name="trash" /></button>
        </div>
      </div>
      <div class="pdiv"></div>

      <TextProps v-if="selectedElement.type === 'text'" :element="selectedElement" />
      <CodeProps v-else-if="selectedElement.type === 'barcode' || selectedElement.type === 'qrcode'" :element="selectedElement" />
      <MediaProps v-else-if="selectedElement.type === 'image' || selectedElement.type === 'icon'" :element="selectedElement" />
      <ShapeProps v-else :element="selectedElement" />

      <div v-if="metric?.text" class="metric" :class="metric.tone" style="margin-top: 10px">{{ metric.text }}</div>
      <div class="pdiv"></div>
      <details>
        <summary class="hint" style="cursor: pointer">查看元素 JSON</summary>
        <pre class="code">{{ json }}</pre>
      </details>
    </div>
  </aside>
</template>

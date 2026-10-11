<script setup lang="ts">
import { designer } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'

/**
 * 快捷键说明：操作和按键。
 */
const SHORTCUTS: ReadonlyArray<[string, string]> = [
  ['撤销 / 重做', 'Ctrl+Z / Ctrl+Y'],
  ['复制 / 粘贴', 'Ctrl+C / Ctrl+V'],
  ['原地复制一份', 'Ctrl+D'],
  ['全选', 'Ctrl+A'],
  ['删除', 'Delete'],
  ['微调 0.1mm / 1mm', '方向键 / Shift+方向键'],
  ['多选', 'Shift+单击 或 框选'],
  ['等比缩放', '拖控制点时按 Shift'],
  ['暂时不吸附', '拖动时按住 Alt'],
  ['编辑文本', '双击文本 或 Enter'],
  ['插入字段', '编辑文本时输入 {{'],
  ['锁定 / 解锁', 'Ctrl+L'],
  ['上移 / 下移一层', 'Ctrl+] / Ctrl+['],
  ['置顶 / 置底', 'Ctrl+Shift+] / Ctrl+Shift+['],
  ['平移画布', '空格+拖动 或 滚轮'],
  ['缩放', 'Ctrl+滚轮'],
  ['放大 / 缩小', 'Ctrl+= / Ctrl+-'],
  ['适应窗口 / 实际大小', 'Ctrl+0 / Ctrl+1'],
  ['保存草稿', 'Ctrl+S'],
  ['取消选择', 'Esc'],
]
</script>

<template>
  <div v-if="designer.ui.shortcutsOpen" class="modal-back" @click.self="designer.ui.shortcutsOpen = false">
    <div class="modal" role="dialog" aria-modal="true" aria-labelledby="shortcuts-title">
      <div class="modal-h">
        <h3 id="shortcuts-title">快捷键</h3>
        <button class="icon-btn" type="button" aria-label="关闭" @click="designer.ui.shortcutsOpen = false"><AppIcon name="x" /></button>
      </div>
      <div class="modal-b">
        <div class="shortcut-grid">
          <template v-for="[action, keys] in SHORTCUTS" :key="action">
            <span>{{ action }}</span><span class="kbd">{{ keys }}</span>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { align, allSelectedLocked, designer, distribute, duplicate, order, redo, removeSelected, toggleLockSelection, undo, zoomActual, zoomFit, zoomIn, zoomOut } from '../stores/designer.ts'
import AppIcon from './AppIcon.vue'

/**
 * 选中的元素个数（决定对齐、分布按钮是否可用）。
 * @returns 个数。
 */
const count = () => designer.selection.length
</script>

<template>
  <div class="toolbar" role="toolbar" aria-label="设计工具">
    <button class="icon-btn" type="button" data-tip="撤销 Ctrl+Z" aria-label="撤销" :disabled="!designer.canUndo" @click="undo()"><AppIcon name="undo" /></button>
    <button class="icon-btn" type="button" data-tip="重做 Ctrl+Y" aria-label="重做" :disabled="!designer.canRedo" @click="redo()"><AppIcon name="redo" /></button>
    <span class="tb-sep"></span>
    <button class="icon-btn" type="button" data-tip="左对齐" aria-label="左对齐" :disabled="!count()" @click="align('left')"><AppIcon name="alignL" /></button>
    <button class="icon-btn" type="button" data-tip="水平居中" aria-label="水平居中" :disabled="!count()" @click="align('hcenter')"><AppIcon name="alignCH" /></button>
    <button class="icon-btn" type="button" data-tip="右对齐" aria-label="右对齐" :disabled="!count()" @click="align('right')"><AppIcon name="alignR" /></button>
    <button class="icon-btn" type="button" data-tip="顶对齐" aria-label="顶对齐" :disabled="!count()" @click="align('top')"><AppIcon name="alignT" /></button>
    <button class="icon-btn" type="button" data-tip="垂直居中" aria-label="垂直居中" :disabled="!count()" @click="align('vcenter')"><AppIcon name="alignCV" /></button>
    <button class="icon-btn" type="button" data-tip="底对齐" aria-label="底对齐" :disabled="!count()" @click="align('bottom')"><AppIcon name="alignB" /></button>
    <button class="icon-btn tb-opt" type="button" data-tip="水平等距（至少 3 个）" aria-label="水平等距" :disabled="count() < 3" @click="distribute('h')"><AppIcon name="distH" /></button>
    <button class="icon-btn tb-opt" type="button" data-tip="垂直等距（至少 3 个）" aria-label="垂直等距" :disabled="count() < 3" @click="distribute('v')"><AppIcon name="distV" /></button>
    <span class="tb-sep"></span>
    <button class="icon-btn" type="button" data-tip="置顶 Ctrl+Shift+]" aria-label="置顶" :disabled="!count()" @click="order('front')"><AppIcon name="toFront" /></button>
    <button class="icon-btn tb-opt" type="button" data-tip="上移一层 Ctrl+]" aria-label="上移一层" :disabled="!count()" @click="order('up')"><AppIcon name="forward" /></button>
    <button class="icon-btn tb-opt" type="button" data-tip="下移一层 Ctrl+[" aria-label="下移一层" :disabled="!count()" @click="order('down')"><AppIcon name="backward" /></button>
    <button class="icon-btn" type="button" data-tip="置底 Ctrl+Shift+[" aria-label="置底" :disabled="!count()" @click="order('back')"><AppIcon name="toBack" /></button>
    <span class="tb-sep"></span>
    <button class="icon-btn" type="button" data-tip="锁定 / 解锁 Ctrl+L" aria-label="锁定或解锁" :class="{ on: allSelectedLocked }" :disabled="!count()" @click="toggleLockSelection()">
      <AppIcon :name="allSelectedLocked ? 'lock' : 'unlock'" />
    </button>
    <button class="icon-btn" type="button" data-tip="复制一份 Ctrl+D" aria-label="复制一份" :disabled="!count()" @click="duplicate()"><AppIcon name="copy" /></button>
    <button class="icon-btn" type="button" data-tip="删除 Delete" aria-label="删除" :disabled="!count()" @click="removeSelected()"><AppIcon name="trash" /></button>
    <span class="spacer"></span>
    <button class="icon-btn" type="button" data-tip="显示网格" aria-label="显示网格" :class="{ on: designer.ui.showGrid }" @click="designer.ui.showGrid = !designer.ui.showGrid"><AppIcon name="grid" /></button>
    <button class="icon-btn" type="button" data-tip="吸附到网格和参考线（拖动时按住 Alt 暂时关闭）" aria-label="吸附" :class="{ on: designer.ui.snap }" @click="designer.ui.snap = !designer.ui.snap"><AppIcon name="magnet" /></button>
    <span class="tb-sep"></span>
    <div class="seg" role="group" aria-label="内容显示方式">
      <button type="button" :class="{ on: designer.ui.mode === 'sample' }" @click="designer.ui.mode = 'sample'">样例值</button>
      <button type="button" :class="{ on: designer.ui.mode === 'placeholder' }" @click="designer.ui.mode = 'placeholder'">占位符</button>
    </div>
    <span class="tb-sep"></span>
    <button class="icon-btn" type="button" data-tip="缩小 Ctrl+-" aria-label="缩小" @click="zoomOut()"><AppIcon name="zoomOut" /></button>
    <button class="zoom-read" type="button" data-tip="实际大小 Ctrl+1" @click="zoomActual()">{{ Math.round(designer.ui.zoom * 100) }}%</button>
    <button class="icon-btn" type="button" data-tip="放大 Ctrl+=" aria-label="放大" @click="zoomIn()"><AppIcon name="zoomIn" /></button>
    <button class="icon-btn" type="button" data-tip="适应窗口 Ctrl+0" aria-label="适应窗口" @click="zoomFit()"><AppIcon name="fit" /></button>
  </div>
</template>

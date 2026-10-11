<script setup lang="ts">
/**
 * 元素库：基础元素、GB/T 191 图标、图片素材。点击添加到视图中心，或拖到画布上指定位置。
 */
import { ELEMENT_META } from '../model/elements.ts'
import { LABEL_ICONS } from '../model/icons.ts'
import { ELEMENT_TYPES } from '../model/template.ts'
import { assets, uploadAsset } from '../stores/assets.ts'
import { addElement, toast } from '../stores/designer.ts'
import { can } from '../stores/session.ts'
import { endDrag, startDrag } from '../stores/drag.ts'
import AppIcon from './AppIcon.vue'

async function onUpload(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    await uploadAsset(file)
    toast('已上传，点击或拖到画布使用')
  } catch (error) {
    toast((error as Error).message, true)
  }
}
</script>

<template>
  <div class="pbody">
    <div class="sect">
      <div class="sect-h"><span>基础元素</span><span class="muted">点击或拖入画布</span></div>
      <div class="tiles">
        <div
          v-for="type in ELEMENT_TYPES"
          :key="type"
          class="tile"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startDrag($event, { kind: 'element', type })"
          @dragend="endDrag()"
          @click="addElement(type)"
          @keydown.enter="addElement(type)"
        >
          <AppIcon :name="ELEMENT_META[type].icon" small />{{ ELEMENT_META[type].label }}
        </div>
      </div>
    </div>

    <div class="sect">
      <div class="sect-h"><span>包装图示标志</span><span class="muted">GB/T 191</span></div>
      <div class="icon-grid">
        <div
          v-for="(icon, key) in LABEL_ICONS"
          :key="key"
          class="icon-cell"
          :data-tip="icon.name"
          :aria-label="icon.name"
          role="button"
          tabindex="0"
          draggable="true"
          @dragstart="startDrag($event, { kind: 'icon', icon: key, name: icon.name })"
          @dragend="endDrag()"
          @click="addElement('icon', { icon: key, name: icon.name })"
          @keydown.enter="addElement('icon', { icon: key, name: icon.name })"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path :d="icon.d"></path></svg>
        </div>
      </div>
    </div>

    <div class="sect">
      <div class="sect-h"><span>图片素材</span><span class="muted">素材库，所有业务共用</span></div>
      <div class="asset-grid">
        <div
          v-for="asset in assets"
          :key="asset.key"
          class="asset"
          :title="asset.name"
          role="button"
          tabindex="0"
          draggable="true"
          @dragstart="startDrag($event, { kind: 'asset', id: asset.key, name: asset.name })"
          @dragend="endDrag()"
          @click="addElement('image', { src: `asset:${asset.key}`, name: asset.name })"
        >
          <img :src="asset.src" :alt="asset.name" />
        </div>
        <label v-if="can('res.manage')" class="asset upload">
          <AppIcon name="upload" small />上传图片
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" hidden @change="onUpload" />
        </label>
      </div>
    </div>
  </div>
</template>

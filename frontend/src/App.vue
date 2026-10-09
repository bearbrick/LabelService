<script setup lang="ts">
/**
 * 应用外壳：顶栏（新建、打开、导出模板 JSON）和设计器。
 * 模板库、登录、保存到服务端见 T-014；在那之前模板以 JSON 文件导入导出，草稿自动保存在本机浏览器。
 */
import { onBeforeUnmount, onMounted, ref } from 'vue'
import AppIcon from './components/AppIcon.vue'
import DesignerView from './components/DesignerView.vue'
import { CANVAS_PRESETS } from './model/presets.ts'
import { designer, exportTemplate, importTemplate, newTemplate, openSample, saveDraft, toast } from './stores/designer.ts'

const menuOpen = ref(false)
const fileInput = ref<HTMLInputElement>()

function choose(action: () => void): void {
  menuOpen.value = false
  action()
}

function confirmReplace(): boolean {
  return !designer.template.elements.length || confirm('当前草稿会被替换。需要保留的话先“导出 JSON”。继续吗？')
}

function create(width: number, height: number, name: string): void {
  if (confirmReplace()) newTemplate(width, height, name)
}

function openFile(): void {
  if (confirmReplace()) fileInput.value?.click()
}

async function onFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    importTemplate(await file.text())
    toast(`已打开 ${file.name}`)
  } catch (error) {
    toast(`打开失败：${(error as Error).message}`, true)
  }
}

function download(): void {
  try {
    const json = exportTemplate()
    const link = document.createElement('a')
    link.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }))
    link.download = `${designer.template.code || 'label-template'}.json`
    link.click()
    URL.revokeObjectURL(link.href)
    toast('已导出模板 JSON')
  } catch (error) {
    toast(`不能导出：${(error as Error).message}`, true)
  }
}

function onDocumentClick(): void {
  menuOpen.value = false
}

function onBeforeUnload(event: BeforeUnloadEvent): void {
  if (!designer.dirty) return
  saveDraft()
  event.preventDefault()
}

onMounted(() => {
  document.addEventListener('click', onDocumentClick)
  window.addEventListener('beforeunload', onBeforeUnload)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick)
  window.removeEventListener('beforeunload', onBeforeUnload)
})
</script>

<template>
  <header class="topbar">
    <div class="brand"><AppIcon name="tag" />标签设计器</div>
    <span class="header-divider"></span>
    <div class="doc-title">
      <b>{{ designer.template.name || '未命名模板' }}</b>
      <small>{{ designer.template.code }}</small>
      <small>{{ designer.template.page.width }}×{{ designer.template.page.height }} mm</small>
    </div>
    <span class="spacer"></span>
    <span class="saved">{{ designer.dirty ? '正在保存草稿…' : designer.savedAt ? `草稿已保存 ${designer.savedAt}` : '' }}</span>
    <div style="position: relative">
      <button class="top-btn" type="button" :class="{ on: menuOpen }" aria-haspopup="menu" :aria-expanded="menuOpen" @click.stop="menuOpen = !menuOpen">
        <AppIcon name="folderOpen" small />模板<AppIcon name="chevD" small />
      </button>
      <div v-if="menuOpen" class="menu" role="menu" @click.stop>
        <div class="hint" style="padding: 4px 10px">新建（选择画布）</div>
        <button v-for="preset in CANVAS_PRESETS" :key="preset.name" class="menu-item" type="button" role="menuitem" @click="choose(() => create(preset.width, preset.height, `新模板 ${preset.name}`))">
          <AppIcon name="plus" small />{{ preset.name }}<small>{{ preset.width }}×{{ preset.height }} mm</small>
        </button>
        <div class="menu-sep"></div>
        <button class="menu-item" type="button" role="menuitem" @click="choose(openFile)"><AppIcon name="upload" small />打开模板 JSON…</button>
        <button class="menu-item" type="button" role="menuitem" @click="choose(() => confirmReplace() && openSample())"><AppIcon name="tag" small />打开示例：成品箱标</button>
      </div>
    </div>
    <button class="top-btn" type="button" @click="download"><AppIcon name="download" small />导出 JSON</button>
    <input ref="fileInput" type="file" accept="application/json,.json" hidden @change="onFile" />
  </header>
  <DesignerView />
  <div class="toasts" aria-live="polite">
    <div v-for="item in designer.toasts" :key="item.id" class="toast" :class="{ err: item.error }">
      <AppIcon :name="item.error ? 'alert' : 'check'" small />{{ item.text }}
    </div>
  </div>
</template>

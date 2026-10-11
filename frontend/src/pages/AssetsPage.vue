<script setup lang="ts">
/**
 * 素材与字体：图片素材（上传、改名、删除；内置素材只读），服务端已加载的字体。
 */
import { onMounted, ref } from 'vue'
import { deleteAsset, listFonts, renameAsset } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { fileSize } from '../model/format.ts'
import { cssFontFamily, FONT_OPTIONS } from '../model/fonts.ts'
import { assets, loadAssets, uploadAsset } from '../stores/assets.ts'
import type { Asset } from '../stores/assets.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { can } from '../stores/session.ts'

const tab = ref<'images' | 'fonts'>('images')
const fonts = ref<{ families: string[]; files: string[] }>({ families: [], files: [] })
const renaming = ref('')
const draftName = ref('')

onMounted(async () => {
  try {
    fonts.value = await listFonts()
  } catch (error) {
    toast(errorText(error), true)
  }
})

async function onUpload(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    const asset = await uploadAsset(file)
    toast(`已上传，模板里写 asset:${asset.key}`)
  } catch (error) {
    toast(errorText(error), true)
  }
}

function startRename(asset: Asset): void {
  renaming.value = asset.key
  draftName.value = asset.name
}

async function finishRename(asset: Asset): Promise<void> {
  if (renaming.value !== asset.key) return
  renaming.value = ''
  const name = draftName.value.trim()
  if (!name || name === asset.name) return
  try {
    await renameAsset(asset.key, name)
    asset.name = name
  } catch (error) {
    toast(errorText(error), true)
  }
}

async function remove(asset: Asset): Promise<void> {
  const text = `确定删除“${asset.name}”？引用 asset:${asset.key} 的模板渲染时会报素材不存在，发布前检查也会拦下。`
  if (!(await confirmBox('删除素材', text, '删除', true))) return
  try {
    await deleteAsset(asset.key)
    await loadAssets(true)
  } catch (error) {
    toast(errorText(error), true)
  }
}

function fontLabel(family: string): string {
  return FONT_OPTIONS.find((font) => font.value === family)?.label ?? family
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>资源</span><span class="sep">/</span><span>素材与字体</span></div>
          <h1>素材与字体 <small>ASSETS</small></h1>
          <p>图片素材在设计器里拖入使用，模板里写 asset:编码。字体在服务端渲染和设计器里同时生效，要确认授权允许商用。</p>
        </div>
        <div class="spacer"></div>
        <label v-if="tab === 'images'" class="btn primary" :class="{ disabled: !can('res.manage') }">
          <AppIcon name="upload" small />上传图片
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" hidden :disabled="!can('res.manage')" @change="onUpload" />
        </label>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="素材类型">
          <button type="button" :class="{ on: tab === 'images' }" @click="tab = 'images'">图片 <span class="muted num">{{ assets.length }}</span></button>
          <button type="button" :class="{ on: tab === 'fonts' }" @click="tab = 'fonts'">字体 <span class="muted num">{{ fonts.families.length }}</span></button>
        </div>
        <span class="hint">PNG、JPG、SVG，不超过 1 MB；SVG 上传时会去掉脚本和外部引用。</span>
      </div>
      <div v-if="tab === 'images'" class="cards">
        <div v-for="a in assets" :key="a.key" class="card">
          <div class="thumb" style="background: #ffffff"><img v-if="a.src" :src="a.src" :alt="a.name" style="box-shadow: none; max-height: 120px" /></div>
          <div class="card-body">
            <div class="card-title">
              <input v-if="renaming === a.key" v-model="draftName" class="inp" aria-label="素材名称" @blur="finishRename(a)" @keydown.enter="($event.target as HTMLInputElement).blur()" />
              <b v-else>{{ a.name }}</b>
              <span v-if="a.builtin" class="pill">内置</span>
            </div>
            <div class="card-meta"><span class="mono">asset:{{ a.key }}</span><span>{{ a.builtin ? '随服务发布' : fileSize(a.size) }}</span></div>
            <div v-if="!a.builtin && can('res.manage')" class="card-actions">
              <button class="btn sm" type="button" @click="startRename(a)">重命名</button>
              <button class="btn sm danger" type="button" @click="remove(a)">删除</button>
            </div>
          </div>
        </div>
      </div>
      <template v-else>
        <div class="table-box">
          <div class="tw">
            <table class="dt">
              <thead><tr><th>字体</th><th>预览</th><th>授权</th><th>状态</th></tr></thead>
              <tbody>
                <tr v-for="f in fonts.families" :key="f">
                  <td class="nowrap"><b style="font-weight: 500">{{ fontLabel(f) }}</b><div class="code-chip">{{ f }}</div></td>
                  <td :style="{ fontFamily: cssFontFamily(f), fontSize: '16px' }">成品箱标 Carton 0123</td>
                  <td>SIL OFL 1.1，可免费商用</td>
                  <td><span class="pill ok">已加载</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <p class="hint" style="margin-top: 12px">字体文件：{{ fonts.files.join('、') || '无' }}。微软雅黑、宋体是 Windows 系统字体，授权不允许随服务分发，不要放进字体目录。</p>
      </template>
    </div>
  </div>
</template>

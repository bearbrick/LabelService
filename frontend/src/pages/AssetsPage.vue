<script setup lang="ts">
/**
 * 素材与字体：图片素材（上传、改名、删除；内置素材只读），服务端已加载的字体。
 */
import { onMounted, ref } from 'vue'
import { deleteAsset, renameAsset } from '../api/admin.ts'
import { deleteFont, uploadFont } from '../api/resources.ts'
import type { FontFile } from '../api/resources.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { fileSize } from '../model/format.ts'
import { BUILTIN_FONTS, cssFontFamily, templateFontName } from '../model/fonts.ts'
import { assets, loadAssets, uploadAsset } from '../stores/assets.ts'
import type { Asset } from '../stores/assets.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { applyFonts, fonts, loadFonts } from '../stores/fonts.ts'
import { go } from '../stores/router.ts'
import { can } from '../stores/session.ts'

/**
 * 当前页签：图片素材或字体。
 */
const tab = ref<'images' | 'fonts'>('images')

/**
 * 正在改名的素材 key；没在改名时为空。
 */
const renaming = ref('')

/**
 * 改名输入框里的名称。
 */
const draftName = ref('')

/**
 * 正在上传或删除字体（字体文件大，上传要几秒）。
 */
const fontBusy = ref(false)

onMounted(() => void loadFonts())

/**
 * 上传图片素材。
 * @param event 文件输入框的 change 事件。
 * @returns 上传结束时完成（失败时提示，不抛）。
 */
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

/**
 * 开始改名。
 * @param asset 素材。
 */
function startRename(asset: Asset): void {
  renaming.value = asset.key
  draftName.value = asset.name
}

/**
 * 结束改名：名称有变化才保存。
 * @param asset 素材。
 * @returns 保存结束时完成。
 */
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

/**
 * 删除素材（确认后）。
 * @param asset 素材。
 * @returns 操作结束时完成。
 */
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

/**
 * 字体的显示名称：内置的两种显示中文名，其余用字体文件里的家族名。
 * @param family 字体文件里的家族名。
 * @returns 显示名称。
 */
function fontLabel(family: string): string {
  const name = templateFontName(family)
  return BUILTIN_FONTS.find((font) => font.value === name)?.label ?? family
}

/**
 * 选好字体文件后上传，成功后设计器和生成标签立即能用。
 * @param event 文件输入框的 change 事件。
 * @returns 上传完成时结束。
 */
async function onFontUpload(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  // 超过 50 MB 时服务端直接断开连接，浏览器只会报“连不上”，这里先拦下来给出明确的提示。
  if (file.size > 50 * 1024 * 1024) {
    toast(`${file.name} 超过 50 MB，不能上传`, true)
    return
  }

  fontBusy.value = true
  toast(`正在上传 ${file.name}…`)
  try {
    applyFonts(await uploadFont(file))
    toast(`已上传 ${file.name}，设计器和生成标签可以用了`)
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    fontBusy.value = false
  }
}

/**
 * 删除上传的字体（确认后）。
 * @param file 字体文件。
 * @returns 删除完成时结束。
 */
async function removeFont(file: FontFile): Promise<void> {
  const names = file.families.join('、')
  if (!(await confirmBox('删除字体', `删除 ${file.fileName}（${names}）后，用这个字体的模板生成时会改用默认字体（思源黑体），设计器里也一样。`, '删除', true))) return
  fontBusy.value = true
  try {
    applyFonts(await deleteFont(file.fileName))
    toast(`已删除 ${file.fileName}`)
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    fontBusy.value = false
  }
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>资源</span><span class="sep">/</span><span>素材与字体</span></div>
          <h1>素材与字体 <small>ASSETS</small></h1>
          <p>图片素材在设计器里拖入使用，模板里写 asset:编码。字体由使用单位自己上传（发布包不带字体），在服务端渲染和设计器里同时生效。</p>
        </div>
        <div class="spacer"></div>
        <label v-if="tab === 'images'" class="btn primary" :class="{ disabled: !can('res.manage') }">
          <AppIcon name="upload" small />上传图片
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" hidden :disabled="!can('res.manage')" @change="onUpload" />
        </label>
        <label v-else class="btn primary" :class="{ disabled: !can('res.manage') || fontBusy }">
          <AppIcon name="upload" small />{{ fontBusy ? '正在处理…' : '上传字体' }}
          <input type="file" accept=".ttf,.otf,.ttc,.otc" hidden :disabled="!can('res.manage') || fontBusy" @change="onFontUpload" />
        </label>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="素材类型">
          <button type="button" :class="{ on: tab === 'images' }" @click="tab = 'images'">图片 <span class="muted num">{{ assets.length }}</span></button>
          <button type="button" :class="{ on: tab === 'fonts' }" @click="tab = 'fonts'">字体 <span class="muted num">{{ fonts.list?.items.length ?? 0 }}</span></button>
        </div>
        <span v-if="tab === 'images'" class="hint">PNG、JPG、SVG，不超过 1 MB；SVG 上传时会去掉脚本和外部引用。</span>
        <span v-else class="hint">TTF、OTF、TTC、OTC，不超过 50 MB；上传后不用重启，设计器和生成标签立即可用。</span>
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
        <div v-if="fonts.list && !fonts.list.hasDefaultFamily" class="notice warn" style="margin-bottom: 14px">
          <AppIcon name="alert" />
          <div>还没有默认字体（思源黑体）：模板里没指定字体或字体找不到时会用系统自带字体，中文可能显示成方框。请上传思源黑体（Noto Sans SC，SIL OFL 1.1，可免费商用，在 Google Fonts 或 GitHub 的 noto-cjk 下载）。</div>
        </div>
        <div class="table-box">
          <div class="tw">
            <table class="dt">
              <thead><tr><th>字体</th><th>模板里写</th><th>预览</th><th>文件</th><th>来源</th><th></th></tr></thead>
              <tbody>
                <tr v-for="f in fonts.list?.items ?? []" :key="f.fileName">
                  <td class="nowrap">
                    <b style="font-weight: 500">{{ fontLabel(f.families[0] ?? f.fileName) }}</b>
                    <div v-for="name in f.families" :key="name" class="hint">{{ name }}</div>
                  </td>
                  <td class="nowrap"><span v-for="name in f.families" :key="name" class="code-chip" style="margin-right: 4px">{{ templateFontName(name) }}</span></td>
                  <td :style="{ fontFamily: cssFontFamily(templateFontName(f.families[0] ?? '')), fontSize: '16px' }">成品箱标 Carton 0123</td>
                  <td class="nowrap"><span class="mono">{{ f.fileName }}</span><div class="hint">{{ fileSize(f.size) }}</div></td>
                  <td class="nowrap"><span class="pill" :class="f.uploaded ? 'ok' : ''">{{ f.uploaded ? '上传' : '部署目录' }}</span></td>
                  <td class="nowrap">
                    <button v-if="f.uploaded && can('res.manage')" class="btn sm danger" type="button" :disabled="fontBusy" @click="removeFont(f)">删除</button>
                  </td>
                </tr>
                <tr v-if="fonts.list && !fonts.list.items.length">
                  <td colspan="6" class="hint" style="text-align: center; padding: 24px">还没有字体，点右上角“上传字体”</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <p class="hint" style="margin-top: 12px">上传前请确认字体授权允许装在服务器上使用、嵌入生成的 PDF；微软雅黑、宋体等 Windows 系统字体通常不允许。部署目录里的字体只能由管理员在服务器上增删。</p>
      </template>
    </div>
  </div>
</template>

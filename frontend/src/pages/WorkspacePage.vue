<script setup lang="ts">
/**
 * 模板工作区：标题和状态、发布、导入导出 JSON，下面四个标签页：设计、数据源、接口调试、版本记录。
 * 内容自动保存为服务端草稿；发布后开放接口才使用新内容。
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { errorText } from '../api/http.ts'
import { listLayouts } from '../api/layouts.ts'
import AppIcon from '../components/AppIcon.vue'
import DesignerView from '../components/DesignerView.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import ApiTestPanel from '../components/admin/ApiTestPanel.vue'
import FieldsTab from '../components/workspace/FieldsTab.vue'
import PreviewDialog from '../components/workspace/PreviewDialog.vue'
import PublishDialog from '../components/workspace/PublishDialog.vue'
import VersionsTab from '../components/workspace/VersionsTab.vue'
import { paperName, sizeText, fmtTime } from '../model/format.ts'
import { useAvailableLayouts } from '../composables/useAvailableLayouts.ts'
import { businessOf, catalog, templateState } from '../stores/catalog.ts'
import { designer, exportTemplate, importTemplate, saveDraft, toast } from '../stores/designer.ts'
import { confirmBox, dialogs } from '../stores/dialogs.ts'
import { go, goBiz, openWorkspace, route } from '../stores/router.ts'
import type { WorkspaceView } from '../stores/router.ts'
import { can, inScope } from '../stores/session.ts'
import { canEdit, discardDraft, loadWorkspace, moveToBusiness, workspace } from '../stores/workspace.ts'

/**
 * 发布对话框是否打开。
 */
const publishOpen = ref(false)

/**
 * 服务端预览对话框是否打开。
 */
const previewOpen = ref(false)

/**
 * 换业务分组的对话框是否打开。
 */
const moveOpen = ref(false)

/**
 * 要换到的业务分组 ID。
 */
const moveTarget = ref(0)

/**
 * 导入模板 JSON 用的文件输入框。
 */
const fileInput = ref<HTMLInputElement>()

watch(() => route.code, (code) => code && loadWorkspace(code), { immediate: true })
onBeforeUnmount(() => void saveDraft())

/**
 * 打开的模板的概要。
 */
const detail = computed(() => workspace.detail)

/**
 * 模板所在的业务分组。
 */
const business = computed(() => businessOf(detail.value?.businessId))

/**
 * 模板状态的显示信息。
 */
const state = computed(() => (detail.value ? templateState(detail.value) : null))

/**
 * 模板的纸张尺寸。
 */
const page = computed(() => designer.template.page)

/**
 * 模板种类：A4 等纸张模板，或标签模板。
 */
const kind = computed(() => (paperName(page.value) ? `${paperName(page.value)} 纸张模板` : '标签模板'))

/**
 * 格子尺寸和当前画布相同、已启用的拼版版式（设计书第 4 节“模板详情页列出可用的版式”）。
 */
const layouts = useAvailableLayouts(() => detail.value?.code, () => [page.value.width, page.value.height], listLayouts)

/**
 * 有没有未发布的改动（有草稿或有还没保存的修改）。
 */
const changed = computed(() => !!detail.value && (detail.value.hasDraft || designer.dirty))

/**
 * 能不能发布（有 tpl.publish 且模板在自己的业务范围里）。
 */
const canPublish = computed(() => can('tpl.publish') && !!detail.value && inScope(detail.value.businessId))

/**
 * 标题栏里草稿的保存状态。
 */
const saveText = computed(() => {
  if (designer.readOnly) return ''
  if (designer.saveError) return '草稿保存失败'
  if (designer.saving || designer.dirty) return '正在保存草稿…'
  return designer.savedAt ? `草稿已保存 ${designer.savedAt}` : ''
})

/**
 * 切换页签。
 * @param view 页签。
 */
function setView(view: WorkspaceView): void {
  openWorkspace(route.code, view)
}

/**
 * 导出模板 JSON 文件（有不能导出的问题时提示）。
 */
function download(): void {
  try {
    const link = document.createElement('a')
    link.href = URL.createObjectURL(new Blob([exportTemplate()], { type: 'application/json' }))
    link.download = `${designer.template.code}.json`
    link.click()
    URL.revokeObjectURL(link.href)
    toast('已导出模板 JSON')
  } catch (error) {
    toast(`不能导出：${(error as Error).message}`, true)
  }
}

/**
 * 导入模板 JSON 文件（替换画布、字段和元素，可以撤销）。
 * @param event 文件输入框的 change 事件。
 * @returns 导入结束时完成（失败时提示，不抛）。
 */
async function onFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    importTemplate(await file.text())
    toast(`已导入 ${file.name}，可以撤销`)
  } catch (error) {
    toast(`导入失败：${(error as Error).message}`, true)
  }
}

/**
 * 放弃未发布的改动（确认后），回到最新发布版本。
 * @returns 操作结束时完成。
 */
async function discard(): Promise<void> {
  if (!(await confirmBox('放弃未发布的改动', `草稿会被删除，模板回到 v${detail.value?.latestVersion} 的内容。这一步不能撤销。`, '放弃改动', true))) return
  try {
    await discardDraft()
    toast('已放弃未发布的改动')
  } catch (error) {
    toast(errorText(error), true)
  }
}

/**
 * 打开换业务分组的对话框，默认选当前分组。
 */
function openMove(): void {
  moveTarget.value = detail.value?.businessId ?? catalog.businesses[0]?.id ?? 0
  moveOpen.value = true
}

/**
 * 把模板换到选中的业务分组。
 * @returns 操作结束时完成。
 */
async function move(): Promise<void> {
  try {
    await moveToBusiness(moveTarget.value)
    moveOpen.value = false
    toast(`已移到 ${businessOf(moveTarget.value).name}`)
  } catch (error) {
    toast(errorText(error), true)
  }
}
</script>

<template>
  <section class="tpl-ws">
    <div v-if="workspace.error" class="page-scroll">
      <div class="page-inner">
        <div class="empty box"><AppIcon name="alert" /><span>{{ workspace.error }}</span></div>
      </div>
    </div>
    <template v-else-if="detail">
      <div class="page-heading">
        <div class="breadcrumb">
          <button
            type="button"
            class="icon-btn xs"
            :aria-label="dialogs.navCollapsed ? '展开模板树' : '收起模板树'"
            :data-tip="dialogs.navCollapsed ? '展开模板树' : '收起模板树'"
            @click="dialogs.navCollapsed = !dialogs.navCollapsed"
          >
            <AppIcon :name="dialogs.navCollapsed ? 'chevR' : 'chevL'" small />
          </button>
          <button type="button" @click="goBiz(business.id || 'all')">{{ business.name }}（{{ business.code }}）</button><span class="sep">/</span><span>{{ designer.template.name }}</span>
        </div>
        <div class="title-row">
          <h1>{{ designer.template.name }} <small>{{ kind }}</small></h1>
          <div class="title-actions">
            <span class="save-state" :class="{ err: designer.saveError }" :title="designer.saveError">{{ saveText }}</span>
            <span v-if="state" class="pill" :class="state.cls"><span class="dot"></span>{{ state.text }}</span>
            <span v-if="designer.readOnly" class="pill warn" title="当前账号没有编辑这个模板的权限"><AppIcon name="lock" small />只读</span>
            <button class="icon-btn" type="button" data-tip="导出模板 JSON" aria-label="导出模板 JSON" @click="download"><AppIcon name="download" /></button>
            <button v-if="!designer.readOnly" class="icon-btn" type="button" data-tip="导入模板 JSON（替换当前内容）" aria-label="导入模板 JSON" @click="fileInput?.click()"><AppIcon name="upload" /></button>
            <button class="btn" type="button" @click="previewOpen = true"><AppIcon name="preview" small />服务端预览</button>
            <button v-if="canEdit() && detail.hasDraft && detail.latestVersion > 0" class="btn" type="button" @click="discard"><AppIcon name="undo" small />放弃改动</button>
            <button class="btn primary" type="button" :disabled="!canPublish || !changed" :title="canPublish ? (changed ? '' : '没有未发布的改动') : '当前账号没有发布权限'" @click="publishOpen = true">
              <AppIcon name="publish" small />发布<kbd>v{{ detail.latestVersion + 1 }}</kbd>
            </button>
          </div>
        </div>
        <div class="table-meta">
          <span>编码<b class="mono">{{ detail.code }}</b></span>
          <span
            >业务<b>{{ business.name }}</b
            ><button v-if="canEdit()" class="link" type="button" style="margin-left: 6px" @click="openMove">更换</button></span
          >
          <span>画布<b>{{ sizeText(page) }}</b></span>
          <span>DPI<b>{{ page.dpi ?? 300 }}</b></span>
          <span class="layouts-meta"
            >可用版式<b v-if="!layouts.length" class="muted" title="还没有格子尺寸和这个模板相同的拼版版式">无</b
            ><template v-else
              ><button v-for="view in layouts.slice(0, 3)" :key="view.layout.code" class="link" type="button" :title="view.layout.name + '，' + view.layout.slots.length + ' 格'" @click="go('layouts', view.layout.code)">{{ view.layout.code }}</button
              ><b v-if="layouts.length > 3">等 {{ layouts.length }} 个</b></template
            ></span
          >
          <span>更新<b>{{ detail.updatedBy || '—' }} · {{ fmtTime(detail.updatedAt) }}</b></span>
        </div>
      </div>
      <nav class="tabs" aria-label="模板视图">
        <button type="button" :class="{ on: route.view === 'designer' }" @click="setView('designer')"><AppIcon name="pen" small />设计 <span>{{ designer.template.elements.length }}</span></button>
        <button type="button" :class="{ on: route.view === 'fields' }" @click="setView('fields')"><AppIcon name="braces" small />数据源 <span>{{ designer.template.fields.length }}</span></button>
        <button v-if="can('api.test')" type="button" :class="{ on: route.view === 'api' }" @click="setView('api')"><AppIcon name="api" small />接口调试</button>
        <button type="button" :class="{ on: route.view === 'versions' }" @click="setView('versions')"><AppIcon name="history" small />版本记录 <span>{{ workspace.versions.length }}</span></button>
      </nav>
      <div class="tab-body">
        <div v-if="route.view === 'designer'" class="ws-designer"><DesignerView /></div>
        <FieldsTab v-else-if="route.view === 'fields'" />
        <div v-else-if="route.view === 'api' && can('api.test')" class="page-scroll">
          <div class="page-inner"><ApiTestPanel :code="detail.code" /></div>
        </div>
        <VersionsTab v-else @publish="publishOpen = true" />
      </div>
    </template>
    <div v-else class="page-scroll"><div class="page-inner hint">正在打开模板…</div></div>
    <input ref="fileInput" type="file" accept="application/json,.json" hidden @change="onFile" />
    <PublishDialog v-if="publishOpen" @close="publishOpen = false" />
    <PreviewDialog v-if="previewOpen" @close="previewOpen = false" />
    <ModalDialog v-if="moveOpen" title="更换业务分组" width="min(420px, 100%)" @close="moveOpen = false">
      <p class="hint" style="margin-top: 0">换分组后，调用方按新分组授权，立即生效。</p>
      <div class="form-row">
        <label for="mv-biz">业务</label>
        <select id="mv-biz" v-model.number="moveTarget" class="sel">
          <option v-for="b in catalog.businesses" :key="b.id" :value="b.id">{{ b.name }}（{{ b.code }}）</option>
        </select>
      </div>
      <template #footer>
        <button class="btn" type="button" @click="moveOpen = false">取消</button>
        <button class="btn primary" type="button" :disabled="moveTarget === detail?.businessId" @click="move">更换</button>
      </template>
    </ModalDialog>
  </section>
</template>

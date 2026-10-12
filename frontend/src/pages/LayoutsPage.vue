<script setup lang="ts">
/**
 * 拼版版式（ADR 0005）：`#/layouts` 是版式列表，`#/layouts/编码` 是拼版设计器。
 * 新建时选纸张和一个已发布的模板，格子尺寸取模板尺寸，先按纸张排满再拖动微调；保存后开放接口立即能用。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { errorText } from '../api/http.ts'
import { deleteLayout, getLayout, listLayouts } from '../api/layouts.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import NavToggle from '../components/admin/NavToggle.vue'
import SheetEditor from '../components/sheet/SheetEditor.vue'
import { PAPERS } from '../model/layout.ts'
import { fitGrid, newLayout, suggestCode } from '../model/layoutArrange.ts'
import type { LayoutView } from '../model/layout.ts'
import { fmtTime } from '../model/format.ts'
import { catalog } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go, route } from '../stores/router.ts'
import { can } from '../stores/session.ts'
import { closeLayout, dirty, openLayout, problems, saveLayout, sheet } from '../stores/sheet.ts'

/**
 * 版式列表。
 */
const layouts = ref<LayoutView[]>([])

/**
 * 正在读取列表。
 */
const loading = ref(false)

/**
 * 读取失败的原因。
 */
const error = ref('')

/**
 * 能不能新建、修改、删除版式（res.manage）。
 */
const canEdit = computed(() => can('res.manage'))

/**
 * 发布过的模板（新建版式时可选）。
 */
const published = computed(() => catalog.templates.filter((item) => item.latestVersion > 0))

/**
 * 新建版式的表单。
 */
const form = reactive({
  /**
   * 新建对话框是否打开。
   */
  open: false,
  /**
   * 纸张名称（PAPERS 里的一项，或“自定义”）。
   */
  paper: PAPERS[0].name,
  /**
   * 纸张宽度，mm。
   */
  width: 210,
  /**
   * 纸张高度，mm。
   */
  height: 297,
  /**
   * 格子尺寸取自哪个模板（模板编码）。
   */
  template: '',
  /**
   * 版式编码。
   */
  code: '',
  /**
   * 版式名称。
   */
  name: '',
  /**
   * 表单错误。
   */
  error: '',
})

/**
 * 选中的模板；没选时为 null。
 */
const chosen = computed(() => published.value.find((item) => item.code === form.template) ?? null)

/**
 * 按纸张和模板尺寸最多能排几行几列。
 */
const grid = computed(() => (chosen.value ? fitGrid({ width: form.width, height: form.height }, { width: chosen.value.width, height: chosen.value.height }, false) : null))

/**
 * 重新读取版式列表。
 * @returns 读取结束时完成。
 */
async function refresh(): Promise<void> {
  loading.value = true
  try {
    layouts.value = await listLayouts()
    error.value = ''
  } catch (failure) {
    error.value = errorText(failure)
  } finally {
    loading.value = false
  }
}

/**
 * 地址里有编码时打开设计器（刚新建、还没保存的版式直接用内存里的）。
 * @param code 版式编码；空字符串表示回到列表。
 * @returns 打开或刷新完成时结束。
 */
async function load(code: string): Promise<void> {
  if (!code) {
    closeLayout()
    await refresh()
    return
  }

  if (sheet.layout?.code === code) return
  try {
    openLayout(await getLayout(code))
    error.value = ''
  } catch (failure) {
    error.value = errorText(failure)
  }
}

watch(() => (route.page === 'layouts' ? route.section : null), (code) => code !== null && void load(code), { immediate: true })

/**
 * 打开新建对话框：默认 A4 纵向，选第一个一张纸能放两个以上的模板，并给出建议的编码和名称。
 */
function startNew(): void {
  // 默认选一张纸上能放两个以上的模板（A4 唛头这种整页的模板拼版没有意义）。
  const several = published.value.find((item) => {
    const grid = fitGrid({ width: 210, height: 297 }, { width: item.width, height: item.height }, false)
    return grid.rows * grid.cols > 1
  })
  Object.assign(form, { open: true, paper: PAPERS[0].name, width: 210, height: 297, template: (several ?? published.value[0])?.code ?? '', code: '', name: '', error: '' })
  suggest()
}

/**
 * 换纸张：带出宽高，重新给出建议的编码和名称。
 */
function onPaper(): void {
  const paper = PAPERS.find((item) => item.name === form.paper)
  if (paper) Object.assign(form, { width: paper.width, height: paper.height })
  suggest()
}

/**
 * 按纸张、模板和能排的行列给出建议的编码和名称。
 */
function suggest(): void {
  if (!chosen.value || !grid.value) return
  form.code = suggestCode(form.paper, { width: chosen.value.width, height: chosen.value.height }, grid.value.rows, grid.value.cols)
  form.name = `${form.paper.startsWith('A') ? form.paper.slice(0, 2) : `${form.width}×${form.height}`} · ${chosen.value.width}×${chosen.value.height} · ${grid.value.cols} 列 × ${grid.value.rows} 行`
}

/**
 * 新建版式：编码不能重复，纸张至少放得下一个格子；在内存里打开（还没保存），进入拼版设计器。
 */
function create(): void {
  if (!chosen.value) return
  if (layouts.value.some((item) => item.layout.code === form.code)) {
    form.error = `编码 ${form.code} 已经有了`
    return
  }

  const layout = newLayout(form.code.trim(), form.name.trim(), { width: form.width, height: form.height }, { width: chosen.value.width, height: chosen.value.height })
  if (!layout.slots.length) {
    form.error = '纸张放不下一个格子'
    return
  }

  openLayout({ layout, enabled: true, updatedBy: '', updatedAt: '' }, true, chosen.value.code)
  form.open = false
  go('layouts', layout.code)
}

/**
 * 保存版式（有越界、重叠时不保存并提示第一个问题）。
 * @returns 保存结束时完成。
 */
async function save(): Promise<void> {
  if (!canEdit.value || !sheet.layout) return
  if (problems.value.messages.length) {
    toast('格子越界或重叠，不能保存：' + problems.value.messages[0], true)
    return
  }

  try {
    if (await saveLayout()) toast('版式已保存，开放接口立即能用')
  } catch (failure) {
    toast(errorText(failure), true)
  }
}

/**
 * 回到版式列表（有未保存的改动时先确认）。
 * @returns 操作结束时完成。
 */
async function back(): Promise<void> {
  if (dirty.value && !(await confirmBox('离开拼版设计器', '有还没保存的改动，确定离开？', '离开', true))) return
  closeLayout()
  go('layouts')
}

/**
 * 删除版式（确认后）。
 * @param view 版式。
 * @returns 操作结束时完成。
 */
async function remove(view: LayoutView): Promise<void> {
  if (!(await confirmBox('删除版式', `确定删除“${view.layout.name}”（${view.layout.code}）？之后带这个编码的请求会返回 LAYOUT_NOT_FOUND。`, '删除', true))) return
  try {
    await deleteLayout(view.layout.code)
    await refresh()
  } catch (failure) {
    toast(errorText(failure), true)
  }
}

/**
 * 有未保存的改动时，关闭或刷新页面前让浏览器提示确认。
 * @param event 页面卸载前的事件。
 */
function onBeforeUnload(event: BeforeUnloadEvent): void {
  if (dirty.value) event.preventDefault()
}

onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))
</script>

<template>
  <section v-if="route.section" class="tpl-ws">
    <div v-if="error" class="page-scroll">
      <div class="page-inner">
        <div class="empty box"><AppIcon name="alert" /><span>{{ error }}</span></div>
        <button class="btn" type="button" style="margin-top: 12px" @click="go('layouts')">回到版式列表</button>
      </div>
    </div>
    <template v-else-if="sheet.layout">
      <div class="page-heading">
        <div class="breadcrumb"><NavToggle :auto="true" /><button type="button" @click="back">资源</button><span class="sep">/</span><button type="button" @click="back">拼版版式</button><span class="sep">/</span><span>{{ sheet.layout.name }}</span></div>
        <div class="title-row">
          <h1>{{ sheet.layout.name }} <small>SHEET LAYOUT</small></h1>
          <div class="title-actions">
            <span class="save-state" :class="{ err: problems.messages.length }">{{ problems.messages.length ? `${problems.messages.length} 个问题` : sheet.isNew ? '还没保存' : dirty ? '有未保存的改动' : '已保存' }}</span>
            <span v-if="!canEdit" class="pill warn" title="当前账号没有“管理画布预设和素材”权限"><AppIcon name="lock" small />只读</span>
            <span v-if="!sheet.enabled" class="pill">已停用</span>
            <button class="btn" type="button" @click="back"><AppIcon name="chevL" small />版式列表</button>
            <button v-if="canEdit" class="btn primary" type="button" :disabled="sheet.busy || !!problems.messages.length || !dirty" @click="save"><AppIcon name="check" small />保存 <kbd>Ctrl+S</kbd></button>
          </div>
        </div>
        <div class="table-meta">
          <span>编码<b class="mono">{{ sheet.layout.code }}</b></span>
          <span>纸张<b>{{ sheet.layout.paper.width }} × {{ sheet.layout.paper.height }} mm</b></span>
          <span>格子<b>{{ sheet.layout.slot.width }} × {{ sheet.layout.slot.height }} mm × {{ sheet.layout.slots.length }}</b></span>
        </div>
      </div>
      <div class="tab-body"><div class="ws-designer"><SheetEditor :read-only="!canEdit" @save="save" /></div></div>
    </template>
  </section>

  <div v-else class="page-scroll fixed-head">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>资源</span><span class="sep">/</span><span>拼版版式</span></div>
          <h1>拼版版式 <small>SHEET LAYOUTS</small></h1>
          <p>把做好的单张标签排在 A4 等纸上打印。版式按格子尺寸和模板匹配，同一个模板既能单张打，也能按版式拼版打；调用时传 <span class="mono">layout</span>（版式编码）。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" :disabled="!canEdit" @click="startNew"><AppIcon name="plus" small />新建版式</button>
      </div>
      <div v-if="error" class="empty box"><AppIcon name="alert" /><span>{{ error }}</span></div>
      <div class="table-box fill">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>版式</th><th>纸张</th><th>格子</th><th>可用的模板</th><th>状态</th><th>更新</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr v-if="!layouts.length"><td colspan="7" class="muted">{{ loading ? '正在加载…' : '还没有版式' }}</td></tr>
              <tr v-for="view in layouts" :key="view.layout.code">
                <td><b style="font-weight: 500">{{ view.layout.name }}</b><br /><span class="mono muted">{{ view.layout.code }}</span></td>
                <td class="num nowrap">{{ view.layout.paper.width }} × {{ view.layout.paper.height }} mm</td>
                <td class="num nowrap">{{ view.layout.slot.width }} × {{ view.layout.slot.height }} mm × {{ view.layout.slots.length }}</td>
                <td class="num">{{ published.filter((t) => Math.abs(t.width - view.layout.slot.width) <= 0.05 && Math.abs(t.height - view.layout.slot.height) <= 0.05).length }} 个</td>
                <td><span class="pill" :class="view.enabled ? 'ok' : ''">{{ view.enabled ? '启用' : '停用' }}</span></td>
                <td class="nowrap muted">{{ view.updatedBy }} · {{ fmtTime(view.updatedAt) }}</td>
                <td class="actions">
                  <button class="link" type="button" @click="go('layouts', view.layout.code)">{{ canEdit ? '设计' : '查看' }}</button>
                  <button v-if="canEdit" class="link danger" type="button" @click="remove(view)">删除</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <ModalDialog v-if="form.open" title="新建版式" @close="form.open = false">
      <div class="form-row">
        <label for="ly-paper">纸张</label>
        <select id="ly-paper" v-model="form.paper" class="sel" @change="onPaper"><option v-for="p in PAPERS" :key="p.name">{{ p.name }}</option><option>自定义</option></select>
      </div>
      <div v-if="form.paper === '自定义'" class="form-row">
        <span class="lb muted">尺寸</span>
        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 6px">
          <div class="unit" data-unit="mm"><input v-model.number="form.width" class="inp num" type="number" min="10" max="1000" aria-label="纸宽" @change="suggest" /></div>
          <div class="unit" data-unit="mm"><input v-model.number="form.height" class="inp num" type="number" min="10" max="1000" aria-label="纸高" @change="suggest" /></div>
        </div>
      </div>
      <div class="form-row">
        <label for="ly-tpl">模板</label>
        <select id="ly-tpl" v-model="form.template" class="sel" @change="suggest">
          <option v-for="t in published" :key="t.code" :value="t.code">{{ t.name }}（{{ t.width }}×{{ t.height }} mm）</option>
        </select>
      </div>
      <p class="hint" style="margin-top: 0">格子尺寸取模板的尺寸；能用这个版式的是所有同尺寸的模板。{{ grid ? `按纸张最多 ${grid.cols} 列 × ${grid.rows} 行，先排满，进去再调。` : '' }}</p>
      <div class="form-row"><label for="ly-code">编码</label><input id="ly-code" v-model.trim="form.code" class="inp mono" /></div>
      <div class="form-row"><label for="ly-name">名称</label><input id="ly-name" v-model.trim="form.name" class="inp" /></div>
      <div v-if="!published.length" class="form-err">还没有已发布的模板。</div>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="!chosen || !form.code || !form.name" @click="create">开始排版</button>
      </template>
    </ModalDialog>
  </div>
</template>

<script setup lang="ts">
/**
 * 画布预设：新建模板和设计器里一键选用。修改预设不影响已有模板。
 */
import { computed, reactive, ref } from 'vue'
import { createPreset, deletePreset, updatePreset } from '../api/admin.ts'
import type { Preset } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import { catalog, reloadPresets } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { can } from '../stores/session.ts'

const KINDS = ['标签纸', '办公纸张']
const kind = ref('全部')
const form = reactive({ open: false, id: 0, name: '', kind: KINDS[0], width: 100, height: 60, enabled: true, error: '', busy: false })
const list = computed(() => catalog.presets.filter((p) => kind.value === '全部' || p.kind === kind.value))

function usage(p: Preset): number {
  return catalog.templates.filter((t) => t.width === p.widthMm && t.height === p.heightMm).length
}

function edit(p: Preset | null): void {
  Object.assign(form, p
    ? { open: true, id: p.id, name: p.name, kind: p.kind, width: p.widthMm, height: p.heightMm, enabled: p.enabled, error: '' }
    : { open: true, id: 0, name: '', kind: KINDS[0], width: 100, height: 60, enabled: true, error: '' })
}

async function save(): Promise<void> {
  form.busy = true
  form.error = ''
  const input = { name: form.name, kind: form.kind, width: form.width, height: form.height, enabled: form.enabled }
  try {
    if (form.id) await updatePreset(form.id, input)
    else await createPreset(input)
    await reloadPresets()
    form.open = false
    toast('已保存')
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}

async function remove(p: Preset): Promise<void> {
  if (!(await confirmBox('删除画布预设', `确定删除“${p.name}”？已有模板不受影响。`, '删除', true))) return
  try {
    await deletePreset(p.id)
    await reloadPresets()
  } catch (error) {
    toast(errorText(error), true)
  }
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>资源</span><span class="sep">/</span><span>画布预设</span></div>
          <h1>画布预设 <small>PRESETS</small></h1>
          <p>新建模板和设计器里可以一键选用。标签纸按卷标尺寸，办公纸张用于 A4、A5 唛头和箱单。修改预设不影响已有模板。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" :disabled="!can('res.manage')" @click="edit(null)"><AppIcon name="plus" small />新建预设</button>
      </div>
      <div class="toolbar-row">
        <div class="seg" role="group" aria-label="按类型筛选">
          <button v-for="k in ['全部', ...KINDS]" :key="k" type="button" :class="{ on: kind === k }" @click="kind = k">{{ k }}</button>
        </div>
      </div>
      <div class="table-box">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>名称</th><th>类型</th><th>宽 × 高</th><th>方向</th><th>同尺寸模板</th><th>状态</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr v-for="p in list" :key="p.id">
                <td><b style="font-weight: 500">{{ p.name }}</b></td>
                <td><span class="mini-chip">{{ p.kind }}</span></td>
                <td class="num nowrap">{{ p.widthMm }} × {{ p.heightMm }} mm</td>
                <td>{{ p.widthMm === p.heightMm ? '方形' : p.widthMm > p.heightMm ? '横向' : '纵向' }}</td>
                <td class="num">{{ usage(p) }} 个</td>
                <td><span class="pill" :class="p.enabled ? 'ok' : ''">{{ p.enabled ? '启用' : '停用' }}</span></td>
                <td class="actions">
                  <template v-if="can('res.manage')"><button class="link" type="button" @click="edit(p)">编辑</button><button class="link danger" type="button" @click="remove(p)">删除</button></template>
                  <span v-else class="muted">—</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <ModalDialog v-if="form.open" :title="form.id ? '编辑画布预设' : '新建画布预设'" @close="form.open = false">
      <div class="form-row"><label for="pe-name">名称</label><input id="pe-name" v-model.trim="form.name" class="inp" placeholder="如：A5 唛头、卷标 70×40" /></div>
      <div class="form-row">
        <label for="pe-type">类型</label>
        <select id="pe-type" v-model="form.kind" class="sel"><option v-for="k in KINDS" :key="k">{{ k }}</option></select>
      </div>
      <div class="form-row">
        <span class="lb muted">尺寸</span>
        <div style="display: grid; grid-template-columns: 1fr auto 1fr; gap: 6px; align-items: center">
          <div class="unit" data-unit="mm"><input id="pe-w" v-model.number="form.width" class="inp num" type="number" min="10" max="2000" aria-label="宽" /></div>
          <button class="icon-btn" type="button" aria-label="交换宽高" @click="[form.width, form.height] = [form.height, form.width]"><AppIcon name="refresh" small /></button>
          <div class="unit" data-unit="mm"><input id="pe-h" v-model.number="form.height" class="inp num" type="number" min="10" max="2000" aria-label="高" /></div>
        </div>
      </div>
      <div class="form-row"><span class="lb muted">状态</span><label class="check"><input v-model="form.enabled" type="checkbox" />启用（停用后新建模板时不显示）</label></div>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="form.busy" @click="save">保存</button>
      </template>
    </ModalDialog>
  </div>
</template>

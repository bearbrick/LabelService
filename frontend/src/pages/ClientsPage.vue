<script setup lang="ts">
/**
 * 调用方与密钥：每个业务系统一个调用方和一把 API Key，按业务分组授权，可选 IP 白名单。Key 只在创建和重置时显示一次。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { createClient, listClients, resetClientKey, setClientStatus, updateClient } from '../api/admin.ts'
import type { ApiClient } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import { fmtTime } from '../model/format.ts'
import { businessOf, catalog } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox, showSecret } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { session } from '../stores/session.ts'

/**
 * 调用方列表。
 */
const clients = ref<ApiClient[]>([])

/**
 * 列表加载过（没加载完时不显示“没有调用方”）。
 */
const loaded = ref(false)

/**
 * 能不能授权“全部业务”：业务范围受限的用户只能授权自己范围里的分组（服务端同样检查）；分组列表本来就只有自己范围里的。
 */
const mayGrantAll = computed(() => session.me?.allBusinesses === true)

/**
 * 新建或编辑调用方的表单。
 */
const form = reactive({
  /**
   * 编辑对话框是否打开。
   */
  open: false,
  /**
   * 编辑的调用方 ID；0 表示新建。
   */
  id: 0,
  /**
   * 名称。
   */
  name: '',
  /**
   * 对方系统的应用标识。
   */
  appId: '',
  /**
   * 说明。
   */
  description: '',
  /**
   * 授权全部业务分组。
   */
  allBusinesses: false,
  /**
   * 授权的业务分组 ID。
   */
  businessIds: [] as number[],
  /**
   * IP 白名单；空表示不限制。
   */
  ipAllowlist: '',
  /**
   * 保存失败的原因。
   */
  error: '',
  /**
   * 正在保存。
   */
  busy: false,
})

onMounted(reload)

/**
 * 重新读取调用方列表。
 * @returns 读取结束时完成。
 */
async function reload(): Promise<void> {
  try {
    clients.value = await listClients()
    loaded.value = true
  } catch (error) {
    toast(errorText(error), true)
  }
}

/**
 * 打开编辑对话框。
 * @param c 要编辑的调用方；null 表示新建。
 */
function edit(c: ApiClient | null): void {
  Object.assign(form, c
    ? { open: true, id: c.id, name: c.name, appId: c.appId, description: c.description, allBusinesses: c.allBusinesses, businessIds: [...c.businessIds], ipAllowlist: c.ipAllowlist, error: '' }
    : { open: true, id: 0, name: '', appId: '', description: '', allBusinesses: false, businessIds: [], ipAllowlist: '', error: '' })
}

/**
 * 密钥框里的提示。
 * @param name 调用方名称。
 * @returns 提示文字。
 */
function keyHint(name: string): string {
  return `${name} 的 API Key 只显示这一次，服务端只保存哈希。请现在复制，配置到业务系统的请求头 X-Api-Key。`
}

/**
 * 保存调用方：新建时显示 Key（只显示这一次）。
 * @returns 保存结束时完成。
 */
async function save(): Promise<void> {
  form.busy = true
  form.error = ''
  const input = { name: form.name, appId: form.appId, description: form.description, allBusinesses: form.allBusinesses, businessIds: form.businessIds, ipAllowlist: form.ipAllowlist }
  try {
    if (form.id) {
      await updateClient(form.id, input)
    } else {
      const result = await createClient(input)
      showSecret('调用方已创建', 'API Key：', result.apiKey, keyHint(result.client.name))
    }

    form.open = false
    await reload()
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}

/**
 * 启用或停用调用方（停用前确认）。
 * @param c 调用方。
 * @returns 操作结束时完成。
 */
async function toggle(c: ApiClient): Promise<void> {
  if (c.enabled && !(await confirmBox('停用调用方', `停用后 ${c.name} 的请求立即返回 401。确定停用？`, '停用', true))) return
  try {
    await setClientStatus(c.id, !c.enabled)
    await reload()
  } catch (error) {
    toast(errorText(error), true)
  }
}

/**
 * 重置 Key（确认后），显示新 Key。
 * @param c 调用方。
 * @returns 操作结束时完成。
 */
async function reset(c: ApiClient): Promise<void> {
  if (!(await confirmBox('重置密钥', `重置后 ${c.name} 的旧 Key 立即失效，业务系统要换成新 Key 才能继续调用。确定重置？`, '重置', true))) return
  try {
    const result = await resetClientKey(c.id)
    showSecret('密钥已重置', '新的 API Key：', result.apiKey, keyHint(c.name))
    await reload()
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
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>接口</span><span class="sep">/</span><span>调用方与密钥</span></div>
          <h1>调用方与密钥 <small>API KEYS</small></h1>
          <p>每个业务系统一个调用方和一把 API Key，可以限定它能调用哪些业务的模板。请求头带上 <span class="code-chip">X-Api-Key</span> 即可调用。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" @click="edit(null)"><AppIcon name="plus" small />新建调用方</button>
      </div>
      <div class="table-box">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>调用方</th><th>AppId</th><th>API Key</th><th>可调用的业务</th><th>IP 白名单</th><th>更新</th><th>启用</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr v-for="c in clients" :key="c.id">
                <td><b style="font-weight: 500">{{ c.name }}</b><div class="hint">{{ c.description }}</div></td>
                <td class="code-chip nowrap">{{ c.appId }}</td>
                <td class="code-chip nowrap">{{ c.keyPrefix }}••••••••</td>
                <td>
                  <div class="chip-row">
                    <span v-if="c.allBusinesses" class="mini-chip">全部业务</span>
                    <span v-for="id in c.allBusinesses ? [] : c.businessIds" v-else :key="id" class="mini-chip"><span class="biz-dot" :style="{ background: businessOf(id).color }"></span>{{ businessOf(id).name }}</span>
                  </div>
                </td>
                <td class="mono" style="white-space: pre-line; font-size: 12px">{{ c.ipAllowlist || '不限制' }}</td>
                <td class="nowrap muted">{{ fmtTime(c.updatedAt) }}</td>
                <td><button class="switch" type="button" :class="{ on: c.enabled }" :aria-label="c.enabled ? '停用' : '启用'" @click="toggle(c)"></button></td>
                <td class="actions"><button class="link" type="button" @click="edit(c)">编辑</button><button class="link" type="button" @click="reset(c)">重置密钥</button></td>
              </tr>
              <tr v-if="loaded && !clients.length"><td colspan="8" class="muted" style="text-align: center; padding: 32px">还没有调用方。点“新建调用方”给业务系统生成一把 API Key</td></tr>
            </tbody>
          </table>
        </div>
      </div>
      <p class="hint" style="margin-top: 12px">密钥只在创建和重置时显示一次，服务端只保存哈希。停用后，这个调用方的请求会返回 401。</p>
    </div>
    <ModalDialog v-if="form.open" :title="form.id ? '编辑调用方' : '新建调用方'" @close="form.open = false">
      <div class="form-row"><label for="cf-name">名称</label><input id="cf-name" v-model.trim="form.name" class="inp" placeholder="如：MES 生产执行系统" /></div>
      <div class="form-row"><label for="cf-app">AppId</label><input id="cf-app" v-model.trim="form.appId" class="inp mono" placeholder="如：mes-prod" /></div>
      <div class="form-row"><label for="cf-desc">说明</label><input id="cf-desc" v-model.trim="form.description" class="inp" placeholder="部署位置、对接人" /></div>
      <div class="form-row" style="align-items: start">
        <span class="lb muted" style="padding-top: 4px">可调用</span>
        <div style="display: grid; gap: 6px">
          <label class="check" :title="mayGrantAll ? '' : '只有业务范围是“全部业务”的用户才能授权全部业务'"><input id="cf-all" v-model="form.allBusinesses" type="checkbox" :disabled="!mayGrantAll" />全部业务（包括以后新建的）</label>
          <div v-if="!form.allBusinesses" class="check-list">
            <label v-for="b in catalog.businesses" :key="b.id" class="check"><input v-model="form.businessIds" type="checkbox" :value="b.id" /><span class="biz-dot" :style="{ background: b.color }"></span>{{ b.name }}</label>
          </div>
        </div>
      </div>
      <div class="form-row" style="align-items: start">
        <label for="cf-ip" style="padding-top: 6px">IP 白名单</label>
        <textarea id="cf-ip" v-model="form.ipAllowlist" class="inp mono" rows="2" placeholder="每行一个，支持 CIDR（如 10.0.0.0/8），留空表示不限制"></textarea>
      </div>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="form.busy" @click="save">{{ form.id ? '保存' : '创建并生成密钥' }}</button>
      </template>
    </ModalDialog>
  </div>
</template>

<script setup lang="ts">
/**
 * 业务分组：模板按业务分组管理，用户的业务范围和调用方的授权都按分组。
 */
import { onMounted, reactive, ref } from 'vue'
import { createBusiness, deleteBusiness, listClients, listUsers, updateBusiness } from '../api/admin.ts'
import type { ApiClient, Business, User } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import { catalog, reloadBusinesses } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go, goBiz } from '../stores/router.ts'
import { can, session } from '../stores/session.ts'

const COLORS = ['#00939a', '#5b6cd6', '#c77c02', '#b24c8e', '#3d8c40', '#d2553f', '#6b7a89']
const users = ref<User[]>([])
const clients = ref<ApiClient[]>([])
const form = reactive({ open: false, id: 0, code: '', name: '', description: '', ownerUserId: null as number | null, color: COLORS[0], error: '', busy: false })

onMounted(async () => {
  try {
    if (can('sys.user')) users.value = await listUsers()
    if (can('api.manage')) clients.value = await listClients()
  } catch (error) {
    toast(errorText(error), true)
  }
})

function ownerName(id: number | null): string {
  if (!id) return '—'
  if (id === session.me?.id) return session.me.name
  return users.value.find((u) => u.id === id)?.name ?? '—'
}

function clientsOf(id: number): ApiClient[] {
  return clients.value.filter((c) => c.allBusinesses || c.businessIds.includes(id))
}

function edit(b: Business | null): void {
  Object.assign(form, b
    ? { open: true, id: b.id, code: b.code, name: b.name, description: b.description, ownerUserId: b.ownerUserId, color: b.color, error: '' }
    : { open: true, id: 0, code: '', name: '', description: '', ownerUserId: session.me?.id ?? null, color: COLORS[catalog.businesses.length % COLORS.length], error: '' })
}

async function save(): Promise<void> {
  form.busy = true
  form.error = ''
  const input = { code: form.code, name: form.name, description: form.description, ownerUserId: form.ownerUserId, color: form.color }
  try {
    if (form.id) await updateBusiness(form.id, input)
    else await createBusiness(input)
    await reloadBusinesses()
    form.open = false
    toast('已保存')
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}

async function remove(b: Business): Promise<void> {
  if (!(await confirmBox('删除业务分组', `确定删除“${b.name}”？分组里还有模板时不能删除。`, '删除', true))) return
  try {
    await deleteBusiness(b.id)
    await reloadBusinesses()
    toast(`已删除 ${b.name}`)
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
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>模板</span><span class="sep">/</span><span>业务分组</span></div>
          <h1>业务分组 <small>BUSINESS</small></h1>
          <p>模板按业务分组管理。用户的权限和调用方的密钥都可以限定到业务，互不影响。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" :disabled="!can('biz.manage')" @click="edit(null)"><AppIcon name="plus" small />新建业务</button>
      </div>
      <div class="table-box">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>业务</th><th>编码</th><th>说明</th><th>模板</th><th>负责人</th><th v-if="can('api.manage')">可调用的系统</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr v-for="b in catalog.businesses" :key="b.id">
                <td class="nowrap"><span class="mini-chip"><span class="biz-dot" :style="{ background: b.color }"></span>{{ b.name }}</span></td>
                <td class="code-chip">{{ b.code }}</td>
                <td class="muted">{{ b.description }}</td>
                <td class="num"><button class="link" type="button" @click="goBiz(b.id)">{{ b.templates }} 个</button></td>
                <td class="nowrap">{{ ownerName(b.ownerUserId) }}</td>
                <td v-if="can('api.manage')">
                  <div class="chip-row">
                    <span v-for="c in clientsOf(b.id)" :key="c.id" class="mini-chip">{{ c.name }}</span><span v-if="!clientsOf(b.id).length" class="muted">—</span>
                  </div>
                </td>
                <td class="actions">
                  <template v-if="can('biz.manage')">
                    <button class="link" type="button" @click="edit(b)">编辑</button><button class="link danger" type="button" @click="remove(b)">删除</button>
                  </template>
                  <span v-else class="muted">—</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <ModalDialog v-if="form.open" :title="form.id ? '编辑业务' : '新建业务'" @close="form.open = false">
      <div class="form-row"><label for="bf-name">名称</label><input id="bf-name" v-model.trim="form.name" class="inp" placeholder="如：出口唛头" /></div>
      <div class="form-row"><label for="bf-code">编码</label><input id="bf-code" v-model.trim="form.code" class="inp mono" placeholder="大写字母开头，如 EXPORT" :disabled="!!form.id" /></div>
      <div class="form-row"><label for="bf-desc">说明</label><input id="bf-desc" v-model.trim="form.description" class="inp" placeholder="这个业务下放哪些标签" /></div>
      <div v-if="users.length" class="form-row">
        <label for="bf-owner">负责人</label>
        <select id="bf-owner" v-model="form.ownerUserId" class="sel">
          <option :value="null">（不指定）</option>
          <option v-for="u in users.filter((x) => x.status === 'active')" :key="u.id" :value="u.id">{{ u.name }}（{{ u.account }}）</option>
        </select>
      </div>
      <div class="form-row">
        <span class="lb muted">标识色</span>
        <div class="swatches">
          <button v-for="c in COLORS" :key="c" type="button" class="swatch" :class="{ on: form.color === c }" :style="{ background: c }" :aria-label="'颜色 ' + c" @click="form.color = c"></button>
        </div>
      </div>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="form.busy" @click="save">保存</button>
      </template>
    </ModalDialog>
  </div>
</template>

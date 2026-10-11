<script setup lang="ts">
/**
 * 角色权限：权限矩阵，勾选即时生效；系统管理员拥有全部权限，不能修改。内置角色、有人在用的角色不能删。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { createRole, deleteRole, listPermissions, updateRole } from '../api/admin.ts'
import type { PermissionInfo, Role } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import { catalog, reloadRoles } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { refreshMe, session } from '../stores/session.ts'

const permissions = ref<PermissionInfo[]>([])
const groups = computed(() => [...new Set(permissions.value.map((p) => p.group))])
const form = reactive({ open: false, name: '', description: '', copyFrom: '', error: '', busy: false })

onMounted(async () => {
  try {
    permissions.value = await listPermissions()
  } catch (error) {
    toast(errorText(error), true)
  }
})

async function toggle(role: Role, key: string, checked: boolean): Promise<void> {
  const next = checked ? [...role.permissions, key] : role.permissions.filter((p) => p !== key)
  try {
    await updateRole(role.id, { name: role.name, description: role.description, permissions: next })
    await reloadRoles()
    if (session.me?.role.id === role.id) await refreshMe()
  } catch (error) {
    toast(errorText(error), true)
    await reloadRoles()
  }
}

async function create(): Promise<void> {
  form.busy = true
  form.error = ''
  try {
    await createRole(form.copyFrom ? { name: form.name, description: form.description, copyFromRoleId: Number(form.copyFrom) } : { name: form.name, description: form.description, permissions: [] })
    await reloadRoles()
    form.open = false
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}

async function remove(role: Role): Promise<void> {
  if (!(await confirmBox('删除角色', `确定删除角色“${role.name}”？`, '删除', true))) return
  try {
    await deleteRole(role.id)
    await reloadRoles()
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
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>权限管理</span><span class="sep">/</span><span>角色权限</span></div>
          <h1>角色权限 <small>RBAC</small></h1>
          <p>勾选即时生效，已登录的用户下一次操作时按新权限校验。系统管理员拥有全部权限，不能修改。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" @click="Object.assign(form, { open: true, name: '', description: '', copyFrom: '', error: '' })"><AppIcon name="plus" small />新建角色</button>
      </div>
      <div class="table-box">
        <div class="tw">
          <table class="dt matrix">
            <thead>
              <tr>
                <th>权限</th>
                <th v-for="r in catalog.roles" :key="r.id">{{ r.name }}<div class="hint" style="font-weight: 400">{{ r.users }} 人</div></th>
              </tr>
            </thead>
            <tbody>
              <template v-for="g in groups" :key="g">
                <tr class="grp"><td :colspan="catalog.roles.length + 1">{{ g }}</td></tr>
                <tr v-for="p in permissions.filter((x) => x.group === g)" :key="p.key">
                  <td>{{ p.label }}<div class="code-chip">{{ p.key }}</div></td>
                  <td v-for="r in catalog.roles" :key="r.id + p.key">
                    <input
                      type="checkbox"
                      :checked="r.allPermissions || r.permissions.includes(p.key)"
                      :disabled="r.allPermissions"
                      :aria-label="r.name + '：' + p.label"
                      @change="toggle(r, p.key, ($event.target as HTMLInputElement).checked)"
                    />
                  </td>
                </tr>
              </template>
              <tr>
                <td class="muted">说明</td>
                <td v-for="r in catalog.roles" :key="'d' + r.id" class="hint" style="text-align: center">
                  {{ r.description }}
                  <div v-if="!r.builtin && !r.users"><button class="link" type="button" style="color: var(--danger)" @click="remove(r)">删除角色</button></div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <ModalDialog v-if="form.open" title="新建角色" @close="form.open = false">
      <div class="form-row"><label for="rf-name">名称</label><input id="rf-name" v-model.trim="form.name" class="inp" placeholder="如：质检组长" /></div>
      <div class="form-row"><label for="rf-desc">说明</label><input id="rf-desc" v-model.trim="form.description" class="inp" /></div>
      <div class="form-row">
        <label for="rf-copy">权限来源</label>
        <select id="rf-copy" v-model="form.copyFrom" class="sel">
          <option value="">从空白开始</option>
          <option v-for="r in catalog.roles" :key="r.id" :value="String(r.id)">复制“{{ r.name }}”的权限</option>
        </select>
      </div>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="form.busy" @click="create">创建</button>
      </template>
    </ModalDialog>
  </div>
</template>

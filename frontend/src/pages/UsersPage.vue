<script setup lang="ts">
/**
 * 用户管理：分配角色和业务范围，停用、重置密码（临时密码只显示一次，用户首次登录必须改）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { createUser, listUsers, resetPassword, setUserStatus, updateUser } from '../api/admin.ts'
import type { User } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import ModalDialog from '../components/admin/ModalDialog.vue'
import { fmtTime } from '../model/format.ts'
import { businessOf, catalog, reloadRoles, roleName } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { confirmBox, showSecret } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { refreshMe, session } from '../stores/session.ts'

/**
 * 全部用户。
 */
const users = ref<User[]>([])

/**
 * 列表的筛选条件。
 */
const filter = reactive({
  /**
   * 搜索：姓名或账号。
   */
  q: '',
  /**
   * 按角色筛选（角色 ID 的文本，空表示全部）。
   */
  roleId: '',
  /**
   * 按状态筛选：active、disabled，空表示全部。
   */
  status: '',
})

/**
 * 新建或编辑用户的表单。
 */
const form = reactive({
  /**
   * 编辑对话框是否打开。
   */
  open: false,
  /**
   * 编辑的用户 ID；0 表示新建。
   */
  id: 0,
  /**
   * 账号（只在新建时填）。
   */
  account: '',
  /**
   * 姓名。
   */
  name: '',
  /**
   * 角色 ID。
   */
  roleId: 0,
  /**
   * 能访问全部业务分组。
   */
  allBusinesses: false,
  /**
   * 能访问的业务分组 ID。
   */
  businessIds: [] as number[],
  /**
   * 保存失败的原因。
   */
  error: '',
  /**
   * 正在保存。
   */
  busy: false,
})

/**
 * 按筛选条件过滤后的用户。
 */
const list = computed(() => {
  const q = filter.q.toLowerCase()
  return users.value.filter((u) => (!q || u.name.toLowerCase().includes(q) || u.account.includes(q)) && (!filter.roleId || u.roleId === Number(filter.roleId)) && (!filter.status || u.status === filter.status))
})

/**
 * 编辑的是不是自己（不能改自己的角色）。
 */
const isSelf = computed(() => form.id === session.me?.id)

onMounted(reload)

/**
 * 重新读取用户列表。
 * @returns 读取结束时完成。
 */
async function reload(): Promise<void> {
  try {
    users.value = await listUsers()
  } catch (error) {
    toast(errorText(error), true)
  }
}

/**
 * 打开编辑对话框；新建时默认选第一个不是系统管理员的角色。
 * @param u 要编辑的用户；null 表示新建。
 */
function edit(u: User | null): void {
  Object.assign(form, u
    ? { open: true, id: u.id, account: u.account, name: u.name, roleId: u.roleId, allBusinesses: u.allBusinesses, businessIds: [...u.businessIds], error: '' }
    : { open: true, id: 0, account: '', name: '', roleId: catalog.roles.find((r) => !r.allPermissions)?.id ?? catalog.roles[0]?.id ?? 0, allBusinesses: false, businessIds: [], error: '' })
}

/**
 * 临时密码框里的提示。
 * @param name 用户姓名。
 * @returns 提示文字。
 */
function tempHint(name: string): string {
  return `临时密码只显示这一次。请当面或通过安全渠道告诉 ${name}，首次登录时必须修改。`
}

/**
 * 保存用户：新建时显示临时密码；改的是自己时刷新当前用户。
 * @returns 保存结束时完成。
 */
async function save(): Promise<void> {
  form.busy = true
  form.error = ''
  const input = { account: form.account, name: form.name, roleId: form.roleId, allBusinesses: form.allBusinesses, businessIds: form.businessIds }
  try {
    if (form.id) {
      await updateUser(form.id, input)
      if (isSelf.value) await refreshMe()
    } else {
      const result = await createUser(input)
      showSecret('用户已创建', `${result.user.name}（${result.user.account}）的临时密码：`, result.temporaryPassword, tempHint(result.user.name))
    }

    form.open = false
    await Promise.all([reload(), reloadRoles()])
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}

/**
 * 重置密码（确认后），显示新的临时密码。
 * @param u 用户。
 * @returns 操作结束时完成。
 */
async function reset(u: User): Promise<void> {
  if (!(await confirmBox('重置密码', `给 ${u.name} 生成新的临时密码，旧密码立即失效，同时解除登录锁定。`, '重置'))) return
  try {
    const result = await resetPassword(u.id)
    showSecret('密码已重置', `${u.name}（${u.account}）的临时密码：`, result.temporaryPassword, tempHint(u.name))
    await reload()
  } catch (error) {
    toast(errorText(error), true)
  }
}

/**
 * 启用或停用用户（停用前确认）。
 * @param u 用户。
 * @returns 操作结束时完成。
 */
async function toggle(u: User): Promise<void> {
  const enable = u.status !== 'active'
  if (!enable && !(await confirmBox('停用用户', `停用后 ${u.name} 立即不能登录和操作。`, '停用', true))) return
  try {
    await setUserStatus(u.id, enable)
    await reload()
  } catch (error) {
    toast(errorText(error), true)
  }
}
</script>

<template>
  <div class="page-scroll fixed-head">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>权限管理</span><span class="sep">/</span><span>用户管理</span></div>
          <h1>用户管理 <small>RBAC</small></h1>
          <p>给每个人分配角色和业务范围。业务范围决定能看到、能编辑哪些业务的模板。</p>
        </div>
        <div class="spacer"></div>
        <button class="btn primary" type="button" @click="edit(null)"><AppIcon name="plus" small />新建用户</button>
      </div>
      <div class="toolbar-row">
        <input id="uf-q" v-model.trim="filter.q" class="inp grow" placeholder="搜索姓名或账号" aria-label="搜索用户" />
        <select id="uf-role" v-model="filter.roleId" class="sel" aria-label="角色">
          <option value="">全部角色</option>
          <option v-for="r in catalog.roles" :key="r.id" :value="String(r.id)">{{ r.name }}</option>
        </select>
        <select id="uf-status" v-model="filter.status" class="sel" aria-label="状态"><option value="">全部状态</option><option value="active">启用</option><option value="disabled">停用</option></select>
      </div>
      <div class="table-box fill">
        <div class="tw" tabindex="0" role="region" aria-label="用户表格">
          <table class="dt users-table">
            <colgroup><col style="width: 240px" /><col style="width: 160px" /><col style="width: 260px" /><col style="width: 70px" /><col style="width: 100px" /><col style="width: 190px" /></colgroup>
            <thead><tr><th>用户</th><th>角色</th><th>业务范围</th><th>状态</th><th>最近登录</th><th class="actions">操作</th></tr></thead>
            <tbody>
              <tr v-for="u in list" :key="u.id">
                <td class="nowrap">
                  <div style="display: flex; gap: 10px; align-items: center">
                    <span class="avatar">{{ u.name.slice(0, 1) }}</span>
                    <div>
                      <b style="font-weight: 500">{{ u.name }}</b><span v-if="u.id === session.me?.id" class="pill accent" style="margin-left: 6px">我</span>
                      <span v-if="u.mustChangePassword" class="pill warn" style="margin-left: 6px">待改密码</span>
                      <div class="code-chip">{{ u.account }}</div>
                    </div>
                  </div>
                </td>
                <td class="nowrap">{{ roleName(u.roleId) }}</td>
                <td>
                  <div class="chip-row">
                    <span v-if="u.allBusinesses" class="mini-chip">全部业务</span>
                    <span v-for="id in u.allBusinesses ? [] : u.businessIds" v-else :key="id" class="mini-chip"><span class="biz-dot" :style="{ background: businessOf(id).color }"></span>{{ businessOf(id).name }}</span>
                  </div>
                </td>
                <td><span class="pill" :class="u.status === 'active' ? 'ok' : ''">{{ u.status === 'active' ? '启用' : '停用' }}</span></td>
                <td class="nowrap muted">{{ u.lastLoginAt ? fmtTime(u.lastLoginAt) : '从未登录' }}</td>
                <td class="actions">
                  <button class="link" type="button" @click="edit(u)">编辑</button>
                  <button class="link" type="button" @click="reset(u)">重置密码</button>
                  <button class="link" :class="{ danger: u.status === 'active' }" type="button" :disabled="u.id === session.me?.id" @click="toggle(u)">{{ u.status === 'active' ? '停用' : '启用' }}</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <ModalDialog v-if="form.open" :title="form.id ? '编辑用户' : '新建用户'" @close="form.open = false">
      <div class="form-row"><label for="uf-name">姓名</label><input id="uf-name" v-model.trim="form.name" class="inp" /></div>
      <div class="form-row"><label for="uf-account">账号</label><input id="uf-account" v-model.trim="form.account" class="inp mono" placeholder="小写字母开头，3～32 位，如 zhangming" :disabled="!!form.id" /></div>
      <div class="form-row">
        <label for="uf-roleid">角色</label>
        <select id="uf-roleid" v-model.number="form.roleId" class="sel" :disabled="isSelf"><option v-for="r in catalog.roles" :key="r.id" :value="r.id">{{ r.name }}</option></select>
      </div>
      <div class="form-row" style="align-items: start">
        <span class="lb muted" style="padding-top: 4px">业务范围</span>
        <div style="display: grid; gap: 6px">
          <label class="check"><input id="uf-all" v-model="form.allBusinesses" type="checkbox" />全部业务</label>
          <div v-if="!form.allBusinesses" class="check-list">
            <label v-for="b in catalog.businesses" :key="b.id" class="check"><input v-model="form.businessIds" type="checkbox" :value="b.id" /><span class="biz-dot" :style="{ background: b.color }"></span>{{ b.name }}</label>
          </div>
        </div>
      </div>
      <p v-if="isSelf" class="hint">不能修改自己的角色。</p>
      <p v-if="!form.id" class="hint">创建后生成临时密码，用户首次登录时必须修改。</p>
      <div v-if="form.error" class="form-err">{{ form.error }}</div>
      <template #footer>
        <button class="btn" type="button" @click="form.open = false">取消</button>
        <button class="btn primary" type="button" :disabled="form.busy" @click="save">{{ form.id ? '保存' : '创建' }}</button>
      </template>
    </ModalDialog>
  </div>
</template>

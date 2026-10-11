<script setup lang="ts">
/**
 * 个人设置：姓名、手机号、修改密码、最近登录。首次登录或被重置密码后只能进这里，改完密码才能用其他功能。
 */
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { changePassword, searchOpLogs, updateProfile } from '../api/admin.ts'
import type { OpLog } from '../api/admin.ts'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { fmtDateTime } from '../model/format.ts'
import { businessOf } from '../stores/catalog.ts'
import { toast } from '../stores/designer.ts'
import { go, route } from '../stores/router.ts'
import { can, refreshMe, session } from '../stores/session.ts'

/**
 * 当前用户。
 */
const me = computed(() => session.me!)

/**
 * 个人信息的表单。
 */
const profile = reactive({
  /**
   * 姓名。
   */
  name: me.value.name,
  /**
   * 手机号。
   */
  phone: me.value.phone,
  /**
   * 正在保存。
   */
  busy: false,
})

/**
 * 修改密码的表单。
 */
const pwd = reactive({
  /**
   * 原密码。
   */
  old: '',
  /**
   * 新密码。
   */
  next: '',
  /**
   * 再输一次新密码。
   */
  confirm: '',
  /**
   * 修改失败的原因。
   */
  error: '',
  /**
   * 正在修改。
   */
  busy: false,
})

/**
 * 最近 8 次登录（没有 log.view 权限时为空）。
 */
const logins = ref<OpLog[]>([])

/**
 * 业务范围的显示文字。
 */
const scope = computed(() => (me.value.allBusinesses ? '全部业务' : me.value.businessIds.map((id) => businessOf(id).name).join('、') || '无'))

onMounted(async () => {
  if (route.section === 'pwd' || me.value.mustChangePassword) {
    await nextTick()
    document.getElementById('pw-old')?.focus()
  }

  if (can('log.view')) logins.value = await searchOpLogs({ userId: me.value.id, type: '登录', pageSize: 8 }).catch(() => [])
})

/**
 * 保存姓名和手机号。
 * @returns 保存结束时完成。
 */
async function saveProfile(): Promise<void> {
  profile.busy = true
  try {
    await updateProfile(profile.name, profile.phone)
    await refreshMe()
    toast('已保存')
  } catch (error) {
    toast(errorText(error), true)
  } finally {
    profile.busy = false
  }
}

/**
 * 修改密码：两次输入要一致；首次登录改完后进入工作台。
 * @returns 修改结束时完成。
 */
async function savePassword(): Promise<void> {
  pwd.error = ''
  if (pwd.next !== pwd.confirm) {
    pwd.error = '两次输入的新密码不一样'
    return
  }

  pwd.busy = true
  try {
    const first = me.value.mustChangePassword
    await changePassword(pwd.old, pwd.next)
    Object.assign(pwd, { old: '', next: '', confirm: '' })
    await refreshMe()
    toast('密码已修改')
    if (first) go('dashboard')
  } catch (error) {
    pwd.error = errorText(error)
  } finally {
    pwd.busy = false
  }
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>个人设置</span></div>
          <h1>个人设置 <small>PROFILE</small></h1>
          <p>修改显示名称和登录密码。角色和业务范围由系统管理员分配。</p>
        </div>
      </div>
      <div v-if="me.mustChangePassword" class="notice warn"><AppIcon name="lock" /><div>这是首次登录或密码刚被重置，请先修改密码，改完才能使用其他功能。</div></div>
      <div class="grid-2">
        <div class="stack">
          <div class="box">
            <h3><AppIcon name="user" small />基本信息</h3>
            <fieldset class="fs" :disabled="me.mustChangePassword">
              <div class="form-row"><label for="pf-name">姓名</label><input id="pf-name" v-model.trim="profile.name" class="inp" /></div>
              <div class="form-row"><label for="pf-phone">手机号</label><input id="pf-phone" v-model.trim="profile.phone" class="inp" placeholder="可不填" /></div>
            </fieldset>
            <div class="form-row"><span class="lb muted">账号</span><span class="code-chip">{{ me.account }}</span></div>
            <div class="form-row"><span class="lb muted">角色</span><span>{{ me.role.name }}</span></div>
            <div class="form-row"><span class="lb muted">业务范围</span><span>{{ scope }}</span></div>
            <div style="display: flex; justify-content: flex-end">
              <button class="btn primary" type="button" :disabled="profile.busy || me.mustChangePassword" @click="saveProfile">保存</button>
            </div>
          </div>
          <div id="pwd-box" class="box">
            <h3><AppIcon name="lock" small />修改密码</h3>
            <form @submit.prevent="savePassword">
              <input type="text" autocomplete="username" :value="me.account" hidden />
              <div class="form-row"><label for="pw-old">当前密码</label><input id="pw-old" v-model="pwd.old" class="inp" type="password" autocomplete="current-password" /></div>
              <div class="form-row"><label for="pw-new">新密码</label><input id="pw-new" v-model="pwd.next" class="inp" type="password" autocomplete="new-password" placeholder="至少 8 位，包含字母和数字" /></div>
              <div class="form-row"><label for="pw-confirm">确认新密码</label><input id="pw-confirm" v-model="pwd.confirm" class="inp" type="password" autocomplete="new-password" /></div>
              <div v-if="pwd.error" class="form-err" style="margin-bottom: 10px">{{ pwd.error }}</div>
              <div style="display: flex; justify-content: flex-end"><button class="btn primary" type="submit" :disabled="pwd.busy">修改密码</button></div>
            </form>
          </div>
        </div>
        <div class="box">
          <h3><AppIcon name="clock" small />最近登录</h3>
          <div class="hint" style="margin-bottom: 8px">上次登录：{{ me.lastLoginAt ? fmtDateTime(me.lastLoginAt) : '—' }}</div>
          <div v-for="o in logins" :key="o.id" class="todo">
            <AppIcon name="shield" small /><span style="flex: 1" class="num">{{ fmtDateTime(o.createdAt) }}</span><span class="code-chip">{{ o.ip }}</span>
          </div>
          <p class="hint">发现不是自己的登录，请立即修改密码并联系管理员。</p>
        </div>
      </div>
    </div>
  </div>
</template>

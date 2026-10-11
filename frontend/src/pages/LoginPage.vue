<script setup lang="ts">
/**
 * 登录页：账号密码登录，可选 7 天内自动登录。连续输错 5 次锁定 15 分钟（服务端控制）。
 */
import { reactive } from 'vue'
import { errorText } from '../api/http.ts'
import AppIcon from '../components/AppIcon.vue'
import { toast } from '../stores/designer.ts'
import { session, signIn } from '../stores/session.ts'

/**
 * 登录表单。
 */
const form = reactive({
  /**
   * 账号。
   */
  account: '',
  /**
   * 密码。
   */
  password: '',
  /**
   * 7 天内自动登录。
   */
  remember: false,
  /**
   * 显示密码明文。
   */
  show: false,
  /**
   * 登录失败的原因。
   */
  error: '',
  /**
   * 正在登录。
   */
  busy: false,
})

/**
 * 登录：账号密码不能为空；失败时显示原因。
 * @returns 登录结束时完成。
 */
async function submit(): Promise<void> {
  if (!form.account || !form.password) {
    form.error = '请输入账号和密码'
    return
  }

  form.busy = true
  try {
    await signIn(form.account, form.password, form.remember)
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-shell">
      <div class="login-intro">
        <div class="brand"><AppIcon name="brand" /><b>标签服务</b></div>
        <span class="eyebrow">LABEL SERVICE · 内部工具</span>
        <h1>标签和唛头在网页上设计，<br />业务系统一个接口生成</h1>
        <p>拖拽设计模板，定义数据字段，发布后 MES、WMS、ERP 用模板编码加数据调用，拿回 PDF、PNG 或 ZPL 文件。</p>
        <div class="login-tags"><span>标签纸 / A4 / A5 唛头</span><span>条码按 DPI 对齐</span><span>版本发布</span><span>按业务隔离权限</span></div>
      </div>
      <form class="login-form" novalidate @submit.prevent="submit">
        <span class="eyebrow">ACCOUNT LOGIN</span>
        <h2>登录</h2>
        <p>使用标签服务的账号登录。</p>
        <div v-if="session.notice" class="notice warn"><AppIcon name="alert" /><div>{{ session.notice }}</div></div>
        <label class="lbl">账号<input id="lg-account" v-model.trim="form.account" class="inp" autocomplete="username" placeholder="请输入账号" @input="form.error = ''" /></label>
        <label class="lbl"
          >密码
          <span class="pwd-wrap">
            <input id="lg-pwd" v-model="form.password" class="inp" :type="form.show ? 'text' : 'password'" autocomplete="current-password" placeholder="请输入密码" @input="form.error = ''" />
            <button class="icon-btn" type="button" :aria-label="form.show ? '隐藏密码' : '显示密码'" @click="form.show = !form.show"><AppIcon :name="form.show ? 'eyeOff' : 'eye'" /></button>
          </span>
        </label>
        <div class="login-row">
          <label class="check"><input id="lg-remember" v-model="form.remember" type="checkbox" />7 天内自动登录</label>
          <button class="link" type="button" @click="toast('请联系系统管理员在“用户管理”里重置密码')">忘记密码？</button>
        </div>
        <div v-if="form.error" class="form-err" role="alert" style="margin-bottom: 10px">{{ form.error }}</div>
        <button class="btn primary block" type="submit" :disabled="form.busy">{{ form.busy ? '登录中…' : '登录' }}</button>
      </form>
    </div>
  </div>
</template>

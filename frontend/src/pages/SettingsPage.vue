<script setup lang="ts">
/**
 * 系统信息：系统名称、副标题、所属公司、版权信息、技术支持和公司 Logo。保存前在“效果预览”里看顶栏和登录页的样子。
 * 需要权限点 sys.settings；读取不用登录，所以登录页也会显示这些内容。
 */
import { computed, reactive, ref } from 'vue'
import { errorText } from '../api/http.ts'
import { resetLogo, saveSystemInfo, SYSTEM_INFO_LIMITS, uploadLogo } from '../api/system.ts'
import type { SaveSystemInfo, SystemInfo } from '../api/system.ts'
import AppIcon from '../components/AppIcon.vue'
import BrandMark from '../components/admin/BrandMark.vue'
import { toast } from '../stores/designer.ts'
import { confirmBox } from '../stores/dialogs.ts'
import { go } from '../stores/router.ts'
import { applySystemInfo, system } from '../stores/system.ts'

/**
 * 表单的一项：字段名、标签、输入框提示。
 */
interface FieldSpec {
  /**
   * 对应系统信息的字段。
   */
  key: keyof SaveSystemInfo
  /**
   * 标签。
   */
  label: string
  /**
   * 输入框里的提示。
   */
  placeholder: string
  /**
   * 输入框下面的说明。
   */
  hint: string
}

/**
 * 表单的各项，按页面上的顺序排列。
 */
const FIELDS: FieldSpec[] = [
  { key: 'systemName', label: '系统名称', placeholder: '如：标签服务', hint: '显示在顶栏左上角和浏览器标签页上，必填' },
  { key: 'subtitle', label: '副标题', placeholder: '如：标签设计与生成工作台', hint: '显示在系统名称右边，窄屏时隐藏；不填就不显示' },
  { key: 'companyName', label: '所属公司', placeholder: '如：某某制造有限公司', hint: '鼠标停在 Logo 上时显示' },
  { key: 'copyright', label: '版权信息', placeholder: '如：© 2026 某某制造有限公司 版权所有', hint: '显示在登录页下方和用户菜单底部' },
  { key: 'support', label: '技术支持', placeholder: '如：信息部 分机 8001', hint: '显示在版权信息旁边；登录页点“忘记密码”时也会提示' },
]

/**
 * 正在编辑的文字部分，打开页面时从当前系统信息复制。
 */
const form = reactive<SaveSystemInfo>(copyText(system.info))

/**
 * 正在保存或上传。
 */
const busy = ref(false)

/**
 * 保存失败的原因。
 */
const error = ref('')

/**
 * 选择 Logo 文件的隐藏输入框。
 */
const fileInput = ref<HTMLInputElement | null>(null)

/**
 * 预览用的系统信息：表单里还没保存的文字加上当前的 Logo。
 */
const preview = computed<SystemInfo>(() => ({ ...form, systemName: form.systemName.trim() || '（系统名称）', logoVersion: system.info.logoVersion }))

/**
 * 表单和已保存的内容是否不同。
 */
const dirty = computed(() => FIELDS.some((field) => form[field.key].trim() !== system.info[field.key]))

/**
 * 复制系统信息的文字部分。
 * @param info 系统信息。
 * @returns 文字部分的副本。
 */
function copyText(info: SystemInfo): SaveSystemInfo {
  return { systemName: info.systemName, subtitle: info.subtitle, companyName: info.companyName, copyright: info.copyright, support: info.support }
}

/**
 * 保存文字部分，成功后顶栏、浏览器标签页立即更新。
 * @returns 保存完成时结束。
 */
async function save(): Promise<void> {
  error.value = ''
  if (!form.systemName.trim()) {
    error.value = '系统名称不能为空'
    return
  }

  busy.value = true
  try {
    const info = await saveSystemInfo({ ...form })
    applySystemInfo(info)
    Object.assign(form, copyText(info))
    toast('系统信息已保存')
  } catch (e) {
    error.value = errorText(e)
  } finally {
    busy.value = false
  }
}

/**
 * 放弃没保存的修改，回到当前的系统信息。
 */
function revert(): void {
  Object.assign(form, copyText(system.info))
  error.value = ''
}

/**
 * 选好 Logo 文件后上传，成功后顶栏和登录页立即换成新 Logo。
 * @param event 文件输入框的 change 事件。
 * @returns 上传完成时结束。
 */
async function onLogoPicked(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  // 清空选择，同一个文件改过后还能再选一次
  input.value = ''
  if (!file) return
  busy.value = true
  try {
    applySystemInfo(await uploadLogo(file))
    toast('公司 Logo 已更新')
  } catch (e) {
    toast(errorText(e), true)
  } finally {
    busy.value = false
  }
}

/**
 * 删除上传的 Logo，恢复成占位。
 * @returns 操作完成时结束。
 */
async function restoreLogo(): Promise<void> {
  if (!(await confirmBox('恢复成占位', '删除上传的公司 Logo，顶栏和登录页改为显示“公司 LOGO”占位。', '恢复', true))) return
  busy.value = true
  try {
    applySystemInfo(await resetLogo())
    toast('已恢复成占位')
  } catch (e) {
    toast(errorText(e), true)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>系统管理</span><span class="sep">/</span><span>系统信息</span></div>
          <h1>系统信息 <small>SYSTEM</small></h1>
          <p>系统名称、公司 Logo 和版权信息显示在顶栏、登录页和浏览器标签页上，保存后所有人立即看到。</p>
        </div>
      </div>
      <div class="grid-2">
        <div class="box">
          <h3><AppIcon name="edit" small />基本信息</h3>
          <form @submit.prevent="save">
            <div v-for="field in FIELDS" :key="field.key" class="form-row top">
              <label :for="`si-${field.key}`">{{ field.label }}</label>
              <div>
                <div class="with-count">
                  <input :id="`si-${field.key}`" v-model="form[field.key]" class="inp" :maxlength="SYSTEM_INFO_LIMITS[field.key]" :placeholder="field.placeholder" @input="error = ''" />
                  <span class="field-count">{{ form[field.key].length }}/{{ SYSTEM_INFO_LIMITS[field.key] }}</span>
                </div>
                <div class="hint" style="margin-top: 4px">{{ field.hint }}</div>
              </div>
            </div>
            <div v-if="error" class="form-err" role="alert" style="margin-bottom: 10px">{{ error }}</div>
            <div style="display: flex; justify-content: flex-end; gap: 8px">
              <button class="btn" type="button" :disabled="busy || !dirty" @click="revert">放弃修改</button>
              <button class="btn primary" type="submit" :disabled="busy || !dirty"><AppIcon name="check" small />保存</button>
            </div>
          </form>
        </div>
        <div class="stack">
          <div class="box">
            <h3><AppIcon name="image" small />公司 Logo</h3>
            <div class="logo-stage"><BrandMark :info="preview" /></div>
            <div class="logo-actions">
              <button class="btn" type="button" :disabled="busy" @click="fileInput?.click()"><AppIcon name="upload" small />{{ system.info.logoVersion ? '更换 Logo' : '上传 Logo' }}</button>
              <button v-if="system.info.logoVersion" class="btn ghost" type="button" :disabled="busy" @click="restoreLogo">恢复成占位</button>
              <input ref="fileInput" type="file" accept=".png,.jpg,.jpeg,.svg,image/png,image/jpeg,image/svg+xml" hidden @change="onLogoPicked" />
            </div>
            <p class="hint" style="margin-top: 10px">PNG、JPG、SVG，不超过 1 MB。建议横向、透明背景，显示时高 28 像素、最宽 148 像素，放在白底圆角框里。</p>
          </div>
          <div class="box">
            <h3><AppIcon name="preview" small />效果预览<span v-if="dirty" class="pill warn" style="margin-left: 8px">未保存</span></h3>
            <div class="preview-bar">
              <BrandMark :info="preview" />
              <span v-if="preview.subtitle" class="brand-sub">{{ preview.subtitle }}</span>
            </div>
            <div class="preview-foot">
              <div class="hint" style="margin-bottom: 6px">登录页下方</div>
              <div v-if="preview.copyright || preview.support" class="login-foot">
                <span v-if="preview.copyright">{{ preview.copyright }}</span>
                <span v-if="preview.support">技术支持：{{ preview.support }}</span>
              </div>
              <div v-else class="hint">没有填版权信息和技术支持，登录页下方不显示。</div>
            </div>
            <p class="hint" style="margin-top: 10px">浏览器标签页标题：{{ preview.systemName }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

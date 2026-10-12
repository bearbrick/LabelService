<script setup lang="ts">
/**
 * 系统信息：系统名称、副标题、所属公司、版权信息、技术支持和公司 Logo。保存前在“效果预览”里看顶栏和登录页的样子。
 * 需要权限点 sys.settings；读取不用登录，所以登录页也会显示这些内容。
 */
import { computed, reactive, ref } from 'vue'
import { errorText } from '../api/http.ts'
import { iconUrl, logoUrl, resetIcon, resetLogo, saveSystemInfo, SYSTEM_INFO_LIMITS, uploadIcon, uploadLogo } from '../api/system.ts'
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
  /**
   * 是否多行（用文本框，可以换行）。
   */
  multiline?: boolean
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
  { key: 'loginTitle', label: '登录页标语', placeholder: '登录页左栏的大标题', hint: '登录页左栏的大标题，回车换行', multiline: true },
  { key: 'loginIntro', label: '登录页说明', placeholder: '标语下面的一段说明', hint: '登录页大标题下面的说明；不填就不显示', multiline: true },
]

/**
 * 系统信息里的一张图：完整 Logo 或 Logo 图标。
 */
interface ImageSlot {
  /**
   * 哪一张：logo 是完整 Logo，icon 是图标。
   */
  kind: 'logo' | 'icon'
  /**
   * 标题。
   */
  title: string
  /**
   * 用在哪里。
   */
  usage: string
  /**
   * 没上传时用什么代替。
   */
  fallback: string
}

/**
 * 两张图，按页面上的顺序排列。
 */
const IMAGE_SLOTS: ImageSlot[] = [
  { kind: 'logo', title: '完整 Logo', usage: '登录页左上角，显示得较大；可以带公司名称文字', fallback: '没上传时用 Logo 图标' },
  { kind: 'icon', title: 'Logo 图标', usage: '顶栏左上角和浏览器标签页，显示很小；建议只用图形', fallback: '没上传时用完整 Logo' },
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
 * 已上传的图片地址；没上传时为空字符串。
 * @param kind 哪一张图。
 * @returns 图片地址。
 */
function imageUrl(kind: ImageSlot['kind']): string {
  return kind === 'logo' ? logoUrl(system.info) : iconUrl(system.info)
}

/**
 * 预览用的系统信息：表单里还没保存的文字加上当前的 Logo。
 */
const preview = computed<SystemInfo>(() => ({ ...form, systemName: form.systemName.trim() || '（系统名称）', logoVersion: system.info.logoVersion, iconVersion: system.info.iconVersion }))

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
  return { systemName: info.systemName, subtitle: info.subtitle, companyName: info.companyName, copyright: info.copyright, support: info.support, loginTitle: info.loginTitle, loginIntro: info.loginIntro }
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
 * 选好图片后上传，成功后顶栏、登录页和浏览器标签页立即换成新图。
 * @param slot 哪一张图。
 * @param event 文件输入框的 change 事件。
 * @returns 上传完成时结束。
 */
async function onImagePicked(slot: ImageSlot, event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  // 清空选择，同一个文件改过后还能再选一次
  input.value = ''
  if (!file) return
  busy.value = true
  try {
    applySystemInfo(await (slot.kind === 'logo' ? uploadLogo(file) : uploadIcon(file)))
    toast(`${slot.title}已更新`)
  } catch (e) {
    toast(errorText(e), true)
  } finally {
    busy.value = false
  }
}

/**
 * 删除上传的图片（确认后）。
 * @param slot 哪一张图。
 * @returns 操作完成时结束。
 */
async function restoreImage(slot: ImageSlot): Promise<void> {
  if (!(await confirmBox(`删除${slot.title}`, `删除上传的${slot.title}，${slot.fallback}；两张都没有时显示“公司 LOGO”占位。`, '删除', true))) return
  busy.value = true
  try {
    applySystemInfo(await (slot.kind === 'logo' ? resetLogo() : resetIcon()))
    toast(`已删除${slot.title}`)
  } catch (e) {
    toast(errorText(e), true)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="page-scroll fixed-head">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>系统管理</span><span class="sep">/</span><span>系统信息</span></div>
          <h1>系统信息 <small>SYSTEM</small></h1>
          <p>系统名称、公司 Logo 和版权信息显示在顶栏、登录页和浏览器标签页上，保存后所有人立即看到。</p>
        </div>
      </div>
      <div class="page-body fill">
        <div class="grid-2">
          <div class="box">
            <h3><AppIcon name="edit" small />基本信息</h3>
            <form @submit.prevent="save">
              <div v-for="field in FIELDS" :key="field.key" class="form-row top">
                <label :for="`si-${field.key}`">{{ field.label }}</label>
                <div>
                  <div class="with-count">
                    <textarea v-if="field.multiline" :id="`si-${field.key}`" v-model="form[field.key]" class="inp" rows="2" :maxlength="SYSTEM_INFO_LIMITS[field.key]" :placeholder="field.placeholder" @input="error = ''"></textarea>
                    <input v-else :id="`si-${field.key}`" v-model="form[field.key]" class="inp" :maxlength="SYSTEM_INFO_LIMITS[field.key]" :placeholder="field.placeholder" @input="error = ''" />
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
              <div v-for="slot in IMAGE_SLOTS" :key="slot.kind" class="logo-slot">
                <div class="logo-slot-head"><b>{{ slot.title }}</b><span class="hint">{{ slot.usage }}</span></div>
                <div class="logo-slot-body">
                  <span class="company-logo" :class="{ big: slot.kind === 'logo' }">
                    <img v-if="imageUrl(slot.kind)" :src="imageUrl(slot.kind)" :alt="slot.title" />
                    <span v-else class="logo-placeholder">未上传</span>
                  </span>
                  <div class="logo-actions">
                    <label class="btn" :class="{ disabled: busy }">
                      <AppIcon name="upload" small />{{ imageUrl(slot.kind) ? '更换' : '上传' }}
                      <input type="file" accept=".png,.jpg,.jpeg,.svg,.webp,image/png,image/jpeg,image/svg+xml,image/webp" hidden :disabled="busy" @change="onImagePicked(slot, $event)" />
                    </label>
                    <button v-if="imageUrl(slot.kind)" class="btn ghost" type="button" :disabled="busy" @click="restoreImage(slot)">删除</button>
                  </div>
                </div>
                <div class="hint">{{ slot.fallback }}</div>
              </div>
              <p class="hint" style="margin-top: 10px">PNG、JPG、SVG、WebP，不超过 1 MB。放在白底圆角框里显示，上传前把图片四周的空白裁掉，显示得更大更清楚。</p>
            </div>
            <div class="box">
              <h3><AppIcon name="preview" small />效果预览<span v-if="dirty" class="pill warn" style="margin-left: 8px">未保存</span></h3>
              <div class="preview-bar">
                <BrandMark :info="preview" />
                <span v-if="preview.subtitle" class="brand-sub" :title="preview.subtitle">{{ preview.subtitle }}</span>
              </div>
              <div class="preview-login">
                <BrandMark :info="preview" full />
                <span class="eyebrow">{{ preview.companyName || 'LABEL SERVICE' }}</span>
                <b>{{ preview.loginTitle }}</b>
                <p v-if="preview.loginIntro">{{ preview.loginIntro }}</p>
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
  </div>
</template>

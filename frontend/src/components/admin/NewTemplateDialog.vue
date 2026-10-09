<script setup lang="ts">
/** 新建模板：名称、编码、画布（预设或自定义）、方向、业务、DPI。创建后直接打开设计器。 */
import { computed, reactive } from 'vue'
import { createTemplate } from '../../api/admin.ts'
import type { Preset } from '../../api/admin.ts'
import { errorText } from '../../api/http.ts'
import { catalog, reloadTemplates } from '../../stores/catalog.ts'
import { dialogs } from '../../stores/dialogs.ts'
import { openWorkspace, route } from '../../stores/router.ts'
import ModalDialog from './ModalDialog.vue'

const CODE_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/
const presets = computed(() => catalog.presets.filter((preset) => preset.enabled))
const first = presets.value[0]
const form = reactive({
  name: '',
  code: '',
  presetId: (first?.id ?? 'custom') as number | 'custom',
  width: first?.widthMm ?? 100,
  height: first?.heightMm ?? 60,
  businessId: (typeof route.biz === 'number' ? route.biz : catalog.businesses[0]?.id) ?? 0,
  dpi: 300,
  error: '',
  busy: false,
})

function pick(preset: Preset): void {
  form.presetId = preset.id
  form.width = preset.widthMm
  form.height = preset.heightMm
}

function orient(direction: 'portrait' | 'landscape'): void {
  const [short, long] = [Math.min(form.width, form.height), Math.max(form.width, form.height)]
  form.width = direction === 'portrait' ? short : long
  form.height = direction === 'portrait' ? long : short
}

async function create(): Promise<void> {
  form.error = ''
  if (!form.name.trim()) form.error = '请填写名称'
  else if (!CODE_PATTERN.test(form.code)) form.error = '编码要以字母开头，只含字母、数字、下划线、连字符，最长 64 个字符'
  else if (!(form.width >= 10 && form.width <= 2000 && form.height >= 10 && form.height <= 2000)) form.error = '画布宽高要在 10～2000 mm 之间'
  else if (!form.businessId) form.error = '请选择业务分组'
  if (form.error) return
  form.busy = true
  try {
    const { code } = await createTemplate({ code: form.code, name: form.name.trim(), businessId: form.businessId, width: form.width, height: form.height, dpi: form.dpi })
    await reloadTemplates()
    dialogs.newTemplate = false
    openWorkspace(code)
  } catch (error) {
    form.error = errorText(error)
  } finally {
    form.busy = false
  }
}
</script>

<template>
  <ModalDialog title="新建模板" @close="dialogs.newTemplate = false">
    <div class="form-row"><label for="nt-name">名称</label><input id="nt-name" v-model="form.name" class="inp" placeholder="如：出货箱标" /></div>
    <div class="form-row" style="align-items: start">
      <label for="nt-code" style="padding-top: 5px">编码</label>
      <div>
        <input id="nt-code" v-model.trim="form.code" class="inp mono" placeholder="如：SHIP_BOX" />
        <div class="hint">接口用它找模板，创建后不能再改</div>
      </div>
    </div>
    <div class="form-row" style="align-items: start">
      <span class="lb muted" style="padding-top: 4px">画布</span>
      <div class="preset-chips">
        <button v-for="p in presets" :key="p.id" class="chip" type="button" :class="{ on: form.presetId === p.id }" @click="pick(p)">{{ p.name }}<span class="mono">{{ p.widthMm }}×{{ p.heightMm }}</span></button>
        <button class="chip" type="button" :class="{ on: form.presetId === 'custom' }" @click="form.presetId = 'custom'">自定义</button>
      </div>
    </div>
    <div class="form-row">
      <span class="lb muted">尺寸</span>
      <div class="pgrid" style="margin: 0">
        <div class="unit" data-unit="mm"><input id="nt-w" v-model.number="form.width" class="inp num" type="number" min="10" max="2000" step="0.5" :disabled="form.presetId !== 'custom'" aria-label="宽" /></div>
        <div class="unit" data-unit="mm"><input id="nt-h" v-model.number="form.height" class="inp num" type="number" min="10" max="2000" step="0.5" :disabled="form.presetId !== 'custom'" aria-label="高" /></div>
      </div>
    </div>
    <div class="form-row">
      <span class="lb muted">方向</span>
      <div class="seg" role="group" aria-label="方向">
        <button type="button" :class="{ on: form.height >= form.width }" @click="orient('portrait')">纵向</button>
        <button type="button" :class="{ on: form.width > form.height }" @click="orient('landscape')">横向</button>
      </div>
    </div>
    <div class="form-row">
      <label for="nt-biz">业务</label>
      <select id="nt-biz" v-model.number="form.businessId" class="sel">
        <option v-for="b in catalog.businesses" :key="b.id" :value="b.id">{{ b.name }}</option>
      </select>
    </div>
    <div class="form-row">
      <label for="nt-dpi">DPI</label>
      <select id="nt-dpi" v-model.number="form.dpi" class="sel"><option :value="203">203</option><option :value="300">300</option><option :value="600">600</option></select>
    </div>
    <div v-if="form.error" class="form-err">{{ form.error }}</div>
    <template #footer>
      <button class="btn" type="button" @click="dialogs.newTemplate = false">取消</button>
      <button class="btn primary" type="button" :disabled="form.busy" @click="create">创建并开始设计</button>
    </template>
  </ModalDialog>
</template>

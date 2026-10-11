<script setup lang="ts">
/**
 * 发布：先保存草稿，服务端检查并试渲染计时；没有错误时填发布说明发布。
 */
import { onMounted, reactive, ref } from 'vue'
import type { PublishCheck } from '../../api/admin.ts'
import { errorText } from '../../api/http.ts'
import { designer, toast } from '../../stores/designer.ts'
import { checkBeforePublish, publish, workspace } from '../../stores/workspace.ts'
import AppIcon from '../AppIcon.vue'
import ModalDialog from '../admin/ModalDialog.vue'

const emit = defineEmits<{ close: [] }>()
const check = ref<PublishCheck | null>(null)
const state = reactive({ note: '', checking: true, busy: false, error: '' })
const next = (workspace.detail?.latestVersion ?? 0) + 1

onMounted(async () => {
  try {
    check.value = await checkBeforePublish()
  } catch (error) {
    state.error = errorText(error)
  } finally {
    state.checking = false
  }
})

async function submit(): Promise<void> {
  state.busy = true
  state.error = ''
  try {
    const result = await publish(state.note.trim())
    toast(`已发布 v${result.version}，接口立即使用新版本`)
    emit('close')
  } catch (error) {
    state.error = errorText(error)
  } finally {
    state.busy = false
  }
}
</script>

<template>
  <ModalDialog :title="`发布 v${next}`" @close="emit('close')">
    <div v-if="state.checking" class="hint" style="padding: 8px 0">正在检查并试渲染…</div>
    <template v-else>
      <div v-for="item in check?.errors ?? []" :key="item" class="notice danger"><AppIcon name="alert" /><div>{{ item }}</div></div>
      <div v-for="item in check?.warnings ?? []" :key="item" class="notice warn"><AppIcon name="alert" /><div>{{ item }}</div></div>
      <div v-if="check" class="metric" style="margin-bottom: 12px">
        <div>{{ designer.template.fields.length }} 个字段 · {{ designer.template.elements.length }} 个元素 · {{ designer.template.page.width }}×{{ designer.template.page.height }} mm</div>
        <div v-if="check.renderMs !== null">试渲染 {{ check.renderMs.toFixed(1) }} ms（{{ designer.template.page.dpi ?? 300 }} dpi，样例值）</div>
        <div v-if="check.canPublish">检查通过，可以发布</div>
      </div>
      <label for="pub-note" class="hint">发布说明（可选）</label>
      <textarea id="pub-note" v-model="state.note" class="inp" rows="3" maxlength="500" placeholder="如：箱号改用 Code128，二维码加入批次"></textarea>
      <p class="hint">发布后，接口默认使用这个版本；请求里也可以用 version 指定历史版本。正在调用的业务系统不需要改动。</p>
    </template>
    <div v-if="state.error" class="form-err">{{ state.error }}</div>
    <template #footer>
      <button class="btn" type="button" @click="emit('close')">取消</button>
      <button class="btn primary" type="button" :disabled="state.checking || state.busy || !check?.canPublish" @click="submit">{{ state.busy ? '发布中…' : '发布' }}</button>
    </template>
  </ModalDialog>
</template>

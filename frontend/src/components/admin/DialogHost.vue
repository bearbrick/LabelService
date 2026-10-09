<script setup lang="ts">
/** 显示全局的确认框和一次性密钥框（stores/dialogs.ts）。 */
import { closeConfirm, copyText, dialogs } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'
import ModalDialog from './ModalDialog.vue'
</script>

<template>
  <ModalDialog v-if="dialogs.confirm" :title="dialogs.confirm.title" width="min(440px, 100%)" @close="closeConfirm(false)">
    <p style="margin: 0; white-space: pre-line">{{ dialogs.confirm.text }}</p>
    <template #footer>
      <button class="btn" type="button" @click="closeConfirm(false)">取消</button>
      <button class="btn" :class="dialogs.confirm.danger ? 'danger-solid' : 'primary'" type="button" @click="closeConfirm(true)">{{ dialogs.confirm.ok }}</button>
    </template>
  </ModalDialog>

  <ModalDialog v-if="dialogs.secret" :title="dialogs.secret.title" width="min(520px, 100%)" @close="dialogs.secret = null">
    <p style="margin-top: 0">{{ dialogs.secret.label }}</p>
    <div class="secret">{{ dialogs.secret.value }}</div>
    <div class="notice warn" style="margin-top: 12px"><AppIcon name="alert" /><div>{{ dialogs.secret.hint }}</div></div>
    <template #footer>
      <button class="btn" type="button" @click="copyText(dialogs.secret.value)"><AppIcon name="copy" small />复制</button>
      <button class="btn primary" type="button" @click="dialogs.secret = null">我已保存</button>
    </template>
  </ModalDialog>
</template>

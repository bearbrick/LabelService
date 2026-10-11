<script setup lang="ts">
/**
 * 公司 Logo 加系统名称：顶栏左上角、登录页、系统信息页的预览共用。没上传 Logo 时显示“公司 LOGO”占位。
 */
import { computed } from 'vue'
import { logoUrl } from '../../api/system.ts'
import type { SystemInfo } from '../../api/system.ts'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 要显示的系统信息（系统信息页预览时是还没保存的表单内容）。
   */
  info: SystemInfo
}>()

/**
 * Logo 的图片地址；没上传时为空字符串。
 */
const url = computed(() => logoUrl(props.info))

/**
 * Logo 的替代文字和悬停提示：有公司名称时用公司名称。
 */
const logoText = computed(() => props.info.companyName || '公司 Logo')
</script>

<template>
  <span class="brand-mark">
    <span class="company-logo" :title="logoText">
      <img v-if="url" :src="url" :alt="logoText" />
      <span v-else class="logo-placeholder">公司 LOGO</span>
    </span>
    <span class="sys-name" :title="info.systemName">{{ info.systemName }}</span>
  </span>
</template>

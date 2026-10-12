<script setup lang="ts">
/**
 * 登录页左上角的品牌区：公司 Logo 单独一行（白底圆角卡片，深色背景上也看得清），下面是系统名称和副标题。
 * 有 Logo 时不再重复写公司名称（Logo 里已经有了），没有 Logo 时用公司名称代替。登录页和系统信息页的效果预览共用。
 */
import { computed } from 'vue'
import { iconUrl, logoUrl } from '../../api/system.ts'
import type { SystemInfo } from '../../api/system.ts'

/**
 * 组件参数。
 */
const props = defineProps<{
  /**
   * 要显示的系统信息（系统信息页预览时是还没保存的表单内容）。
   */
  info: SystemInfo
  /**
   * 缩小显示（系统信息页的效果预览）。
   */
  compact?: boolean
}>()

/**
 * Logo 地址：先用完整 Logo，没有时用图标；都没上传时为空字符串。
 */
const logo = computed(() => logoUrl(props.info) || iconUrl(props.info))
</script>

<template>
  <div class="login-brand" :class="{ compact: props.compact }">
    <span v-if="logo" class="login-logo"><img :src="logo" :alt="info.companyName || '公司 Logo'" /></span>
    <span v-else-if="info.companyName" class="eyebrow">{{ info.companyName }}</span>
    <div class="login-product">
      <b :title="info.systemName">{{ info.systemName }}</b>
      <span v-if="info.subtitle">{{ info.subtitle }}</span>
    </div>
  </div>
</template>

<script lang="ts">
// 缩略图按“编码 + 修改时间”缓存成 blob URL，模板改了自动换新的；整个会话共用。
const cache = new Map<string, Promise<string>>()
</script>

<script setup lang="ts">
/** 模板缩略图：服务端用样例值渲染的小 PNG（GET /templates/{code}/thumbnail）；渲染不了时显示占位。 */
import { ref, watch } from 'vue'
import { templateThumbnail } from '../../api/admin.ts'
import AppIcon from '../AppIcon.vue'

const props = defineProps<{ code: string; updatedAt: string; alt: string }>()
const src = ref('')

function fetchThumb(code: string, stamp: string): Promise<string> {
  const key = `${code}|${stamp}`
  let pending = cache.get(key)
  if (!pending) {
    pending = templateThumbnail(code, stamp)
      .then(async (response) => (response.status === 204 ? '' : URL.createObjectURL(await response.blob())))
      .catch(() => '')
    cache.set(key, pending)
  }

  return pending
}

watch(
  () => [props.code, props.updatedAt],
  async ([code, stamp]) => {
    src.value = ''
    const url = await fetchThumb(code, stamp)
    if (code === props.code && stamp === props.updatedAt) src.value = url
  },
  { immediate: true },
)
</script>

<template>
  <img v-if="src" :src="src" :alt="props.alt" />
  <span v-else class="thumb-empty"><AppIcon name="tag" /></span>
</template>

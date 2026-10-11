<script setup lang="ts">
/**
 * 接口调试页：选一个已发布的模板，模拟业务系统调用开放接口。
 */
import { computed, ref, watch } from 'vue'
import AppIcon from '../components/AppIcon.vue'
import ApiTestPanel from '../components/admin/ApiTestPanel.vue'
import { catalog } from '../stores/catalog.ts'
import { go } from '../stores/router.ts'

const code = ref('')
const groups = computed(() =>
  catalog.businesses
    .map((b) => ({ ...b, items: catalog.templates.filter((t) => t.businessId === b.id).sort((x, y) => x.name.localeCompare(y.name, 'zh')) }))
    .filter((g) => g.items.length),
)

watch(
  () => catalog.templates.length,
  () => {
    if (!code.value) code.value = catalog.templates.find((t) => t.latestVersion > 0)?.code ?? ''
  },
  { immediate: true },
)
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="page-head">
        <div>
          <div class="breadcrumb"><button type="button" @click="go('dashboard')">工作台</button><span class="sep">/</span><span>接口</span><span class="sep">/</span><span>接口调试</span></div>
          <h1>接口调试 <small>API</small></h1>
          <p>模拟业务系统调用开放接口，用已发布的模板生成文件。这里用登录身份调用，不需要 API Key，也不记入调用日志。</p>
        </div>
        <div class="spacer"></div>
        <div style="width: min(340px, 100%)">
          <select id="api-tpl" v-model="code" class="sel" aria-label="选择模板">
            <optgroup v-for="g in groups" :key="g.id" :label="g.name">
              <option v-for="t in g.items" :key="t.code" :value="t.code">{{ t.name }}（{{ t.code }}）{{ t.latestVersion ? '' : ' · 未发布' }}</option>
            </optgroup>
          </select>
        </div>
      </div>
      <ApiTestPanel v-if="code" :code="code" />
      <div v-else class="empty box"><AppIcon name="api" /><span>先在右上角选择一个模板</span></div>
    </div>
  </div>
</template>

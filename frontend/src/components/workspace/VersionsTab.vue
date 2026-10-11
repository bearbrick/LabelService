<script setup lang="ts">
/**
 * 版本记录：草稿一行加全部已发布版本；可以把历史版本复制到草稿（回滚第一步，再发布才生效）。
 */
import { computed } from 'vue'
import { errorText } from '../../api/http.ts'
import { fmtDateTime } from '../../model/format.ts'
import { designer, toast } from '../../stores/designer.ts'
import { confirmBox } from '../../stores/dialogs.ts'
import { can } from '../../stores/session.ts'
import { canEdit, restoreVersion, workspace } from '../../stores/workspace.ts'
import AppIcon from '../AppIcon.vue'

/**
 * 组件事件。
 */
const emit = defineEmits<{
  /**
   * 要发布草稿（打开发布对话框）。
   */
  publish: []
}>()
/**
 * 打开的模板的概要。
 */
const detail = computed(() => workspace.detail!)
/**
 * 草稿和最新发布版本不一样（有草稿或有还没保存的修改）。
 */
const changed = computed(() => detail.value.hasDraft || designer.dirty)

/**
 * 把历史版本复制到草稿（确认后）。
 * @param version 版本号。
 * @returns 操作结束时完成（失败时提示，不抛）。
 */
async function restore(version: number): Promise<void> {
  const ok = await confirmBox('复制到草稿', `把 v${version} 的内容复制成草稿，当前草稿会被覆盖。发布后接口才使用它。`, '复制到草稿')
  if (!ok) return
  try {
    await restoreVersion(version)
    toast(`已把 v${version} 复制到草稿`)
  } catch (error) {
    toast(errorText(error), true)
  }
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="section-title">
        <h2>版本记录<span>{{ workspace.versions.length }} 个已发布版本</span></h2>
        <span class="hint">接口默认使用最新发布的版本，请求里也可以用 version 指定历史版本。</span>
      </div>
      <div class="table-box">
        <div class="tw">
          <table class="dt">
            <thead>
              <tr><th>版本</th><th>说明</th><th>发布人</th><th>时间</th><th class="actions">操作</th></tr>
            </thead>
            <tbody>
              <tr>
                <td class="nowrap"><span class="pill warn">草稿</span></td>
                <td>{{ changed ? '有未发布的改动' : '与最新发布版本一致' }}</td>
                <td class="nowrap">{{ detail.updatedBy || '—' }}</td>
                <td class="nowrap num muted">{{ fmtDateTime(detail.updatedAt) }} 保存</td>
                <td class="actions">
                  <button class="link" type="button" :disabled="!can('tpl.publish') || !changed" @click="emit('publish')">发布为 v{{ detail.latestVersion + 1 }}</button>
                </td>
              </tr>
              <tr v-for="v in workspace.versions" :key="v.versionNo">
                <td class="nowrap">
                  <span class="pill" :class="v.versionNo === detail.latestVersion ? 'ok' : ''">v{{ v.versionNo }}</span>
                  <span v-if="v.versionNo === detail.latestVersion" class="hint" style="margin-left: 8px">接口当前使用</span>
                </td>
                <td>{{ v.note || '（无说明）' }}</td>
                <td class="nowrap">{{ v.publishedBy || '—' }}</td>
                <td class="nowrap num muted">{{ fmtDateTime(v.publishedAt) }}</td>
                <td class="actions"><button class="link" type="button" :disabled="!canEdit()" @click="restore(v.versionNo)">复制到草稿</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <div v-if="!workspace.versions.length" class="empty">
        <AppIcon name="history" /><span>还没有发布过。发布后，业务系统才能用 {{ detail.code }} 调用接口。</span>
      </div>
    </div>
  </div>
</template>

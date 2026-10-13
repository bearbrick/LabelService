<script setup lang="ts">
/**
 * 设计页左侧的字段：只用来把字段放到画布上（拖到画布生成“名称：{{key}}”文本，拖到条码上绑定内容，双击插入到选中的文本）。
 * 字段的定义和修改（名称、类型、必填、默认值或自动值、格式、样例值）统一在模板工作区的“数据源”页。
 */
import { computed } from 'vue'
import { describeAuto } from '../model/autoValues.ts'
import { usedKeys } from '../model/bindings.ts'
import type { FieldDefinition } from '../model/template.ts'
import { addField, designer, insertFieldIntoSelection, report, toast } from '../stores/designer.ts'
import { endDrag, startDrag } from '../stores/drag.ts'
import { openWorkspace, route } from '../stores/router.ts'
import AppIcon from './AppIcon.vue'

/**
 * 被元素引用的字段 key。
 */
const used = computed(() => usedKeys(designer.template.elements))

/**
 * 一键添加绑定里引用了但还没定义的字段（字符串类型），其余属性到“数据源”页再改。
 * @param key 字段 key。
 */
function addMissing(key: string): void {
  const error = addField(key, 'string')
  if (error) toast(error, true)
}

/**
 * 鼠标停在字段上时的说明：名称、类型，自动值或样例值。
 * @param field 字段。
 * @returns 提示文字。
 */
function tip(field: FieldDefinition): string {
  const value = field.source === 'template'
    ? field.auto ? `系统预设：${describeAuto(field.auto)}（无需接口传入）` : `模板手写值：${String(field.default ?? '')}`
    : field.auto
    ? `自动值：${describeAuto(field.auto)}（调用方不用传）`
    : `样例值：${field.sample === undefined || field.sample === null || field.sample === '' ? '无' : field.type === 'image' ? '已选图片' : String(field.sample)}`
  return `${field.name || field.key} · ${field.type}\n${value}\n拖到画布或条码上，双击插入到选中的文本`
}

/**
 * 打开“数据源”页修改字段。
 */
function editFields(): void {
  openWorkspace(route.code, 'fields')
}
</script>

<template>
  <div class="pbody">
    <div v-for="key in report.undefinedKeys" :key="key" class="notice warn">
      <AppIcon name="alert" />
      <div>
        模板引用了未定义的字段 <b class="mono">{{ key }}</b>
        <div v-if="!designer.readOnly" style="margin-top: 6px"><button class="btn sm" type="button" @click="addMissing(key)">添加为字段</button></div>
      </div>
    </div>
    <p class="hint" style="margin-top: 0">拖到画布：生成“名称：{{ '{' + '{key}' + '}' }}”文本。<br />拖到一维码、二维码上：直接绑定内容。<br />双击：插入到选中的文本。</p>

    <div
      v-for="field in designer.template.fields"
      :key="field.key"
      class="field-row"
      draggable="true"
      :title="tip(field)"
      @dragstart="startDrag($event, { kind: 'field', key: field.key })"
      @dragend="endDrag()"
      @dblclick.stop="insertFieldIntoSelection(field.key)"
    >
      <span class="k">{{ field.key }}</span><span class="n">{{ field.name }}</span>
      <span v-if="field.source === 'template'" class="pill accent" title="由模板提供，接口无需传入">模板</span>
      <span v-else-if="field.auto" class="pill accent" :title="`调用方没传时服务端自动算：${describeAuto(field.auto)}`">自动</span>
      <span v-if="!used.has(field.key)" class="pill" title="没有元素引用这个字段">未用</span>
      <span class="tpill">{{ field.type }}</span>
    </div>
    <div v-if="!designer.template.fields.length" class="hint" style="padding: 8px 0">还没有字段。</div>

    <div class="field-source">
      <span class="hint">新增、修改字段（类型、必填、默认值或自动值、样例值）在“数据源”页。</span>
      <button class="btn sm" type="button" @click="editFields"><AppIcon name="braces" small />去数据源</button>
    </div>
  </div>
</template>

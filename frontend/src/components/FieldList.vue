<script setup lang="ts">
/**
 * 字段：拖到画布生成“名称：{{key}}”文本，拖到条码上绑定内容，双击插入到选中的文本。
 * 点击字段展开编辑名称、类型、样例值等（完整的字段表在模板工作区的“数据源”页）。
 */
import { computed, reactive, ref } from 'vue'
import { usedKeys } from '../model/bindings.ts'
import type { FieldDefinition, FieldType } from '../model/template.ts'
import { addField, commit, commitSoon, designer, insertFieldIntoSelection, removeField, report, toast } from '../stores/designer.ts'
import { endDrag, startDrag } from '../stores/drag.ts'
import AppIcon from './AppIcon.vue'

/**
 * 字段类型的选项。
 */
const FIELD_TYPES: FieldType[] = ['string', 'number', 'date', 'image']
/**
 * 快速添加字段的输入。
 */
const quick = reactive({
  /**
   * 新字段的 key。
   */
  key: '',
  /**
   * 新字段的类型。
   */
  type: 'string' as FieldType,
  /**
   * 添加失败的原因（key 不合法或重复）。
   */
  error: '',
})
/**
 * 展开编辑的字段 key。
 */
const openKey = ref<string | null>(null)
/**
 * 被元素引用的字段 key。
 */
const used = computed(() => usedKeys(designer.template.elements))

/**
 * 快速添加字段：成功后展开它、清空输入框；失败时显示原因。
 */
function quickAdd(): void {
  quick.error = addField(quick.key, quick.type) ?? ''
  if (!quick.error) {
    openKey.value = quick.key.trim()
    quick.key = ''
  }
}

/**
 * 一键添加绑定里引用了但还没定义的字段（字符串类型）。
 * @param key 字段 key。
 */
function addMissing(key: string): void {
  const error = addField(key, 'string')
  if (error) toast(error, true)
}

/**
 * 样例值在输入框里的文本。
 * @param field 字段。
 * @returns 样例值的文本；没有样例时为空字符串。
 */
function sampleText(field: FieldDefinition): string {
  return field.sample === undefined || field.sample === null ? '' : String(field.sample)
}

/**
 * 样例值按字段类型保存：number 存数字，其他存字符串，空字符串表示没有样例。
 * @param field 字段。
 * @param text 输入的样例值。
 */
function setSample(field: FieldDefinition, text: string): void {
  if (text === '') delete field.sample
  else field.sample = field.type === 'number' && Number.isFinite(Number(text)) ? Number(text) : text
  commitSoon()
}

/**
 * 图片字段选样例图片：不超过 1 MB，读成 data URL 存为样例值。
 * @param field 图片字段。
 * @param event 文件输入框的 change 事件。
 */
function onImageSample(field: FieldDefinition, event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (file.size > 1024 * 1024) {
    toast('图片超过 1 MB', true)
    return
  }

  const reader = new FileReader()
  reader.onload = () => {
    field.sample = String(reader.result)
    commit()
  }
  reader.readAsDataURL(file)
}

/**
 * 删除字段；还被元素引用时先确认。
 * @param field 字段。
 */
function remove(field: FieldDefinition): void {
  if (used.value.has(field.key) && !confirm(`字段 ${field.key} 还被元素引用，删除后这些绑定会显示为未定义。确定删除？`)) return
  removeField(field)
  openKey.value = null
}
</script>

<template>
  <div class="pbody">
    <div v-for="key in report.undefinedKeys" :key="key" class="notice warn">
      <AppIcon name="alert" />
      <div>
        模板引用了未定义的字段 <b class="mono">{{ key }}</b>
        <div style="margin-top: 6px"><button class="btn sm" type="button" @click="addMissing(key)">添加为字段</button></div>
      </div>
    </div>
    <p class="hint" style="margin-top: 0">拖到画布：生成“名称：{{ '{' + '{key}' + '}' }}”文本。<br />拖到一维码、二维码上：直接绑定内容。<br />双击：插入到选中的文本。点击：编辑字段。</p>

    <template v-for="field in designer.template.fields" :key="field.key">
      <div
        class="field-row"
        :class="{ open: openKey === field.key }"
        draggable="true"
        :title="`样例值：${sampleText(field) || '无'}`"
        @dragstart="startDrag($event, { kind: 'field', key: field.key })"
        @dragend="endDrag()"
        @click="openKey = openKey === field.key ? null : field.key"
        @dblclick.stop="insertFieldIntoSelection(field.key)"
      >
        <span class="k">{{ field.key }}</span><span class="n">{{ field.name }}</span>
        <span v-if="!used.has(field.key)" class="pill" title="没有元素引用这个字段">未用</span>
        <span class="tpill">{{ field.type }}</span>
      </div>
      <div v-if="openKey === field.key" class="field-edit">
        <div class="prow"><label :for="`fn-${field.key}`">名称</label><input :id="`fn-${field.key}`" v-model="field.name" class="inp" @change="commit()" /></div>
        <div class="prow">
          <label :for="`ft-${field.key}`">类型</label>
          <select :id="`ft-${field.key}`" v-model="field.type" class="sel" @change="commit()">
            <option v-for="type in FIELD_TYPES" :key="type" :value="type">{{ type }}</option>
          </select>
        </div>
        <div v-if="field.type !== 'image'" class="prow">
          <label :for="`fs-${field.key}`">样例值</label>
          <input :id="`fs-${field.key}`" class="inp" :value="sampleText(field)" @input="setSample(field, ($event.target as HTMLInputElement).value)" />
        </div>
        <div v-else class="prow">
          <span class="lb">样例图</span>
          <label class="btn sm">选择图片<input type="file" accept="image/png,image/jpeg" hidden @change="onImageSample(field, $event)" /></label>
        </div>
        <div v-if="field.type === 'number' || field.type === 'date'" class="prow">
          <label :for="`fm-${field.key}`">格式</label>
          <input :id="`fm-${field.key}`" v-model="field.format" class="inp mono" :placeholder="field.type === 'date' ? 'yyyy-MM-dd' : '0.00'" @change="commit()" />
        </div>
        <div class="prow">
          <span class="lb">必填</span>
          <div style="display: flex; justify-content: space-between; align-items: center">
            <label class="check"><input v-model="field.required" type="checkbox" @change="commit()" />调用时必须传</label>
            <button class="btn sm danger" type="button" @click="remove(field)">删除</button>
          </div>
        </div>
      </div>
    </template>

    <div class="sect" style="margin-top: 14px">
      <div class="sect-h"><span>添加字段</span></div>
      <div style="display: grid; grid-template-columns: minmax(0, 1fr) 84px auto; gap: 6px">
        <input v-model.trim="quick.key" class="inp mono" placeholder="key，如 lotNo" aria-label="字段 key" @keydown.enter="quickAdd()" />
        <select v-model="quick.type" class="sel" aria-label="字段类型">
          <option v-for="type in FIELD_TYPES" :key="type" :value="type">{{ type }}</option>
        </select>
        <button class="btn" type="button" @click="quickAdd()">添加</button>
      </div>
      <div v-if="quick.error" class="hint" style="color: var(--danger); margin-top: 4px">{{ quick.error }}</div>
    </div>
  </div>
</template>

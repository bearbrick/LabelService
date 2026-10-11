<script setup lang="ts">
/**
 * 数据源：声明模板需要的字段（key、名称、类型、必填、默认值、格式、样例值）。
 * 扫描元素里的 {{key}}：未定义的一键添加，没被引用的给出提示；右边按字段自动生成调用样例。
 */
import { computed, reactive } from 'vue'
import { sampleRequest } from '../../model/request.ts'
import type { FieldDefinition, FieldType } from '../../model/template.ts'
import { addField, commit, designer, removeField, renameField, report, toast } from '../../stores/designer.ts'
import { confirmBox, copyText } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'

/**
 * 字段类型的选项。
 */
const FIELD_TYPES: FieldType[] = ['string', 'number', 'date', 'image']

/**
 * 每行 key 的错误（行号 → 原因）。
 */
const keyErrors = reactive<Record<number, string>>({})

/**
 * 按字段生成的调用样例（单张生成的请求体）。
 */
const requestJson = computed(() => JSON.stringify(sampleRequest(designer.template), null, 2))

/**
 * 模板的字段（表格直接编辑它们）。
 */
const fields = computed(() => designer.template.fields)

/**
 * 加一行字段，key 取第一个没用过的 fieldN。
 */
function addRow(): void {
  let index = fields.value.length + 1
  while (fields.value.some((field) => field.key === `field${index}`)) index++
  const error = addField(`field${index}`, 'string')
  if (error) toast(error, true)
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
 * 改字段 key（引用它的元素一起改）；不合法或重复时恢复原值并显示原因。
 * @param field 字段。
 * @param index 行号。
 * @param event 输入框的 change 事件。
 */
function onKeyChange(field: FieldDefinition, index: number, event: Event): void {
  const input = event.target as HTMLInputElement
  const error = renameField(field, input.value)
  keyErrors[index] = error ?? ''
  if (error) input.value = field.key
}

/**
 * 默认值、样例值在输入框里的文本。
 * @param value 值。
 * @returns 文本；没有值时为空字符串。
 */
function text(value: unknown): string {
  return value === undefined || value === null ? '' : String(value)
}

/**
 * 默认值、样例值按类型保存：number 存数字，其他存字符串，空字符串表示没有。
 * @param field 字段。
 * @param prop 改默认值还是样例值。
 * @param value 输入的文本。
 */
function setValue(field: FieldDefinition, prop: 'default' | 'sample', value: string): void {
  if (value === '') delete field[prop]
  else field[prop] = field.type === 'number' && Number.isFinite(Number(value)) ? Number(value) : value
  commit()
}

/**
 * 改字段类型：图片、字符串没有格式，图片的样例值清掉（类型不再匹配）。
 * @param field 字段。
 */
function onTypeChange(field: FieldDefinition): void {
  if (field.type === 'image' || field.type === 'string') delete field.format
  if (field.type === 'image') delete field.sample
  commit()
}

/**
 * 格式输入框的提示。
 * @param type 字段类型。
 * @returns 提示文字。
 */
function formatHint(type: FieldType): string {
  return type === 'number' ? '如 0.00' : type === 'date' ? '如 yyyy-MM-dd' : '—'
}

/**
 * 删除字段；还被元素引用时先确认。
 * @param field 字段。
 * @returns 操作结束时完成。
 */
async function remove(field: FieldDefinition): Promise<void> {
  const used = !report.value.unusedFields.includes(field.key)
  if (used && !(await confirmBox('删除字段', `字段 ${field.key} 还被元素引用，删除后这些绑定会显示为未定义，发布前要处理。确定删除？`, '删除', true))) return
  removeField(field)
}
</script>

<template>
  <div class="page-scroll">
    <div class="page-inner">
      <div class="toolbar-row">
        <button class="btn" type="button" :disabled="designer.readOnly" @click="addRow"><AppIcon name="plus" small />新增字段</button>
        <span class="toolbar-divider"></span>
        <span class="hint">声明模板需要的字段 key 和类型。业务系统调用接口时按这里传数据，样例值只用于设计器预览、发布前试渲染和调用样例。</span>
      </div>
      <div class="two-col">
        <div style="display: grid; gap: 16px; min-width: 0; align-content: start">
          <div v-if="report.undefinedKeys.length || report.unusedFields.length">
            <div v-for="key in report.undefinedKeys" :key="key" class="notice warn">
              <AppIcon name="alert" />
              <div>
                模板里引用了未定义的字段 <b class="mono">{{ key }}</b>，发布前需要补上。
                <button class="btn sm" type="button" :disabled="designer.readOnly" @click="addMissing(key)">添加为字段</button>
              </div>
            </div>
            <div v-if="report.unusedFields.length" class="notice info">
              <AppIcon name="braces" />
              <div>还没有元素引用这些字段：<span class="mono">{{ report.unusedFields.join('、') }}</span>。调用时仍会按必填校验。</div>
            </div>
          </div>
          <div class="box">
            <h3><AppIcon name="braces" small />字段（{{ fields.length }}）</h3>
            <div class="tw">
              <fieldset class="fs" :disabled="designer.readOnly">
                <table class="grid">
                  <thead>
                    <tr>
                      <th style="min-width: 130px">key</th>
                      <th style="min-width: 100px">名称</th>
                      <th style="min-width: 100px">类型</th>
                      <th>必填</th>
                      <th style="min-width: 100px">默认值</th>
                      <th style="min-width: 110px">格式</th>
                      <th style="min-width: 140px">样例值</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(field, i) in fields" :key="i">
                      <td>
                        <input class="inp mono" :class="{ err: keyErrors[i] }" :value="field.key" :aria-label="`字段 ${i + 1} 的 key`" @change="onKeyChange(field, i, $event)" />
                        <div v-if="keyErrors[i]" class="err-msg">{{ keyErrors[i] }}</div>
                      </td>
                      <td><input v-model="field.name" class="inp" aria-label="名称" @change="commit()" /></td>
                      <td>
                        <select v-model="field.type" class="sel" aria-label="类型" @change="onTypeChange(field)">
                          <option v-for="type in FIELD_TYPES" :key="type" :value="type">{{ type }}</option>
                        </select>
                      </td>
                      <td style="text-align: center"><input v-model="field.required" type="checkbox" aria-label="必填" @change="commit()" /></td>
                      <td>
                        <input class="inp" :value="text(field.default)" :placeholder="field.required === false ? '可选' : '—'" aria-label="默认值" @change="setValue(field, 'default', ($event.target as HTMLInputElement).value)" />
                      </td>
                      <td>
                        <input v-model="field.format" class="inp mono" :disabled="field.type === 'string' || field.type === 'image'" :placeholder="formatHint(field.type)" aria-label="格式" @change="commit()" />
                      </td>
                      <td>
                        <input v-if="field.type !== 'image'" class="inp" :value="text(field.sample)" aria-label="样例值" @change="setValue(field, 'sample', ($event.target as HTMLInputElement).value)" />
                        <span v-else class="hint">在设计页的字段面板选择样例图</span>
                      </td>
                      <td><button class="icon-btn xs" type="button" aria-label="删除字段" @click="remove(field)"><AppIcon name="trash" small /></button></td>
                    </tr>
                  </tbody>
                </table>
              </fieldset>
            </div>
            <div v-if="!fields.length" class="hint" style="padding: 12px 0">还没有字段。点“新增字段”，或在设计页的文本里写 {{ '{' + '{key}' + '}' }} 后回来一键添加。</div>
            <p class="hint" style="margin: 12px 0 0">key 以字母开头，只含字母、数字、下划线。改 key 时，模板里引用它的元素会一起改。</p>
          </div>
        </div>
        <div style="display: grid; gap: 16px; min-width: 0; align-content: start">
          <div class="box">
            <div class="code-head">
              <h3 style="margin: 0"><AppIcon name="api" small />调用样例</h3>
              <button class="btn sm" type="button" @click="copyText(requestJson)"><AppIcon name="copy" small />复制</button>
            </div>
            <div class="endpoint"><span class="method">POST</span>/api/v1/render</div>
            <pre class="code">{{ requestJson }}</pre>
          </div>
          <div class="box">
            <h3>字段定义接口</h3>
            <div class="endpoint"><span class="method">GET</span>/api/v1/templates/{{ designer.template.code }}/schema</div>
            <p class="hint" style="margin: 0">返回已发布版本的字段定义和调用样例，供调用方对接。草稿里的字段改动要发布后才对接口生效。</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

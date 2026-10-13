<script setup lang="ts">
/**
 * 数据源：声明字段的名称、类型、必填、来源、互斥的系统预设与手写值、格式和样例值。
 * 扫描元素里的 {{key}}：未定义的一键添加，没被引用的给出提示；接口资料通过按钮抽屉查看。
 */
import { computed, reactive, ref } from 'vue'
import { isValidAuto, parseAuto } from '../../model/autoValues.ts'
import { fieldSource, presetOptions } from '../../model/fieldSources.ts'
import { sampleRequest } from '../../model/request.ts'
import type { FieldDefinition, FieldType } from '../../model/template.ts'
import { addField, commit, designer, removeField, renameField, report, setFieldLiteral, setFieldPreset, setFieldSource, toast } from '../../stores/designer.ts'
import { confirmBox } from '../../stores/dialogs.ts'
import AppIcon from '../AppIcon.vue'
import FieldsApiDrawer from './FieldsApiDrawer.vue'

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
 * 当前打开的接口资料；未打开时不渲染抽屉。
 */
const apiDrawer = ref<'sample' | 'schema' | null>(null)

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
  if (prop === 'default') { setFieldLiteral(field, value); return }
  if (value === '') delete field[prop]
  else field[prop] = field.type === 'number' && Number.isFinite(Number(value)) ? Number(value) : value
  commit()
}

/**
 * 图片字段的样例图：不超过 1 MB，读成 data URL 存为样例值（设计器预览、试渲染和调用样例用）。
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
 * 样例图的地址：存的是 data URL 就直接用，只存了 base64 时补上前缀。
 * @param sample 样例值。
 * @returns 图片地址。
 */
function imageSrc(sample: unknown): string {
  const text = String(sample)
  return text.startsWith('data:') ? text : `data:image/png;base64,${text}`
}

/**
 * 改字段类型：图片、字符串没有格式，图片的样例值清掉（类型不再匹配）；自动值和新类型不匹配时去掉。
 * @param field 字段。
 */
function onTypeChange(field: FieldDefinition): void {
  if (field.type === 'image' || field.type === 'string') delete field.format
  if (field.type === 'image') delete field.sample
  if (field.auto && !isValidAuto(field.type, field.auto)) delete field.auto
  commit()
}

/**
 * 系统预设下拉框的当前选项：空字符串是“无”，否则是自动值预设的 kind。
 * @param field 字段。
 * @returns 选项值。
 */
function autoKind(field: FieldDefinition): string {
  return field.auto ? (parseAuto(field.auto)?.kind ?? '') : ''
}

/**
 * 选择系统预设并清空手写值；选择无时恢复手写输入，模板提供的字段不要求接口填写。
 * @param field 字段。
 * @param kind 选项值。
 */
function onAutoKind(field: FieldDefinition, kind: string): void {
  setFieldPreset(field, kind)
}

/**
 * 改“N 天后”等预设里的 N。
 * @param field 字段。
 * @param value 输入的文本。
 */
function onAutoN(field: FieldDefinition, value: string): void {
  setFieldPreset(field, autoKind(field), Number(value))
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
  <div class="page-scroll fixed-head">
    <div class="page-inner">
      <div class="toolbar-row">
        <button class="btn" type="button" :disabled="designer.readOnly" @click="addRow"><AppIcon name="plus" small />新增字段</button>
        <span class="toolbar-divider"></span>
        <span class="hint">接口字段由业务系统传入；模板提供的系统预设和手写值二选一。</span>
        <span class="fields-toolbar-space"></span>
        <button class="btn" type="button" @click="apiDrawer = 'sample'"><AppIcon name="api" small />调用样例</button>
        <button class="btn" type="button" @click="apiDrawer = 'schema'"><AppIcon name="braces" small />字段定义接口</button>
      </div>
      <div class="page-body fill">
        <div class="fields-full-width">
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
                        <th style="min-width: 160px">数据来源</th>
                        <th style="min-width: 160px">系统预设</th>
                        <th style="min-width: 160px">手写值</th>
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
                        <td style="text-align: center">
                          <span v-if="fieldSource(field) === 'template'" class="hint" title="模板提供不要求接口填写">—</span>
                          <input v-else v-model="field.required" type="checkbox" aria-label="必填" :disabled="!!field.auto" :title="field.auto ? '有自动值，调用方不用传' : ''" @change="commit()" />
                        </td>
                        <td>
                          <select class="sel" :value="fieldSource(field)" aria-label="数据来源" @change="setFieldSource(field, ($event.target as HTMLSelectElement).value as 'input' | 'template')">
                            <option value="input">接口传入</option>
                            <option value="template">模板提供</option>
                            <option v-if="fieldSource(field) === 'legacy'" value="legacy" disabled>兼容：接口优先</option>
                          </select>
                        </td>
                        <td>
                          <div class="default-cell">
                            <select class="sel" :value="autoKind(field)" :disabled="fieldSource(field) === 'input'" aria-label="系统预设" @change="onAutoKind(field, ($event.target as HTMLSelectElement).value)">
                              <option value="">无</option>
                              <option v-for="preset in presetOptions(field)" :key="preset.kind" :value="preset.kind">{{ preset.label }}</option>
                            </select>
                            <input
                              v-if="autoKind(field).includes('N')"
                              class="inp num n-input"
                              type="number"
                              min="1"
                              max="9999"
                              :value="parseAuto(field.auto ?? '')?.n"
                              aria-label="N"
                              title="N：天数、月数或年数（1～9999）"
                              @change="onAutoN(field, ($event.target as HTMLInputElement).value)"
                            />
                          </div>
                        </td>
                        <td>
                          <input class="inp" :value="field.auto ? '' : text(field.default)" :disabled="fieldSource(field) === 'input' || !!field.auto" :placeholder="fieldSource(field) === 'input' ? '由接口传入' : field.auto ? '使用系统预设' : '可留空，如 0 或固定文字'" aria-label="手写值" @change="setValue(field, 'default', ($event.target as HTMLInputElement).value)" />
                        </td>
                        <td>
                          <input v-model="field.format" class="inp mono" :disabled="field.type === 'string' || field.type === 'image'" :placeholder="formatHint(field.type)" aria-label="格式" @change="commit()" />
                        </td>
                        <td>
                          <span v-if="fieldSource(field) === 'template'" class="hint" title="预览直接使用模板提供的值">—</span>
                          <input v-else-if="field.type !== 'image'" class="inp" :value="text(field.sample)" aria-label="样例值" @change="setValue(field, 'sample', ($event.target as HTMLInputElement).value)" />
                          <span v-else class="img-sample">
                            <img v-if="field.sample" :src="imageSrc(field.sample)" alt="样例图" />
                            <label class="btn sm">{{ field.sample ? '换图片' : '选择图片' }}<input type="file" accept="image/png,image/jpeg" hidden @change="onImageSample(field, $event)" /></label>
                            <button v-if="field.sample" class="link" type="button" @click="setValue(field, 'sample', '')">清除</button>
                          </span>
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
        </div>
      </div>
    </div>
  </div>
  <FieldsApiDrawer v-if="apiDrawer" :kind="apiDrawer" :code="designer.template.code" :sample="requestJson" @close="apiDrawer = null" />
</template>

<style scoped>
.fields-full-width { min-width: 0; width: 100%; }
.fields-toolbar-space { flex: 1; }
.page-inner { padding-inline: 20px; }
</style>

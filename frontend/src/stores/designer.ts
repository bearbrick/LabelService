/**
 * 设计器 store 的统一入口：画布、面板、快捷键都从这里导入，不直接改模板。
 * 状态在 designerState.ts，撤销历史和自动保存在 designerHistory.ts，打开、导入导出在 designerTemplate.ts，
 * 元素操作在 designerElements.ts，字段操作在 designerFields.ts。
 * 开发环境下 window.__labelDesigner 就是这个模块（见 main.ts），浏览器里调试和自动化测试用它驱动。
 */
export * from './designerState.ts'
export * from './designerHistory.ts'
export * from './designerTemplate.ts'
export * from './designerElements.ts'
export * from './designerFields.ts'

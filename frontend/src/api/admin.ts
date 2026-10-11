/**
 * 管理接口的统一入口，和 docs/admin-api.md 一一对应。时间都是 UTC 的 ISO 字符串。
 * 按业务分在几个文件里，页面和 store 统一从这里导入。
 */
export * from './accounts.ts'
export * from './businesses.ts'
export * from './clients.ts'
export * from './logs.ts'
export * from './resources.ts'
export * from './templates.ts'

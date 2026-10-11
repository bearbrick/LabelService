import { createApp } from 'vue'
import App from './App.vue'
import * as store from './stores/designer.ts'
import './style.css'
import './admin.css'
import './layout-recovery.css'

createApp(App).mount('#app')

// 开发环境下把 store 挂到 window.__labelDesigner，方便在浏览器控制台里查看和驱动（打包后不存在）。
if (import.meta.env.DEV) Object.assign(window, { __labelDesigner: store })

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    // 开发时把接口请求转给本地服务端（dotnet run --project backend/src/LabelService.Server）。
    proxy: { '/api': 'http://localhost:5080' },
  },
})

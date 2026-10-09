import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    // bwip-js 包含全部码制，压缩后约 1 MB（gzip 约 350 KB）。内网工具可以接受，以后需要时再按码制拆分或延迟加载。
    chunkSizeWarningLimit: 1600,
  },
  server: {
    port: 5173,
    // 开发时把接口请求和字体转给本地服务端（dotnet run --project backend/src/LabelService.Server）。
    proxy: { '/api': 'http://localhost:5080', '/fonts': 'http://localhost:5080' },
  },
})

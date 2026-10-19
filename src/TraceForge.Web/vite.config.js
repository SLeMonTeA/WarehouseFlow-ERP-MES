import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 6173,
    proxy: {
      '/api': { target: 'http://localhost:6102', changeOrigin: true },
      '/hubs': { target: 'ws://localhost:6102', changeOrigin: true, ws: true }
    }
  }
})

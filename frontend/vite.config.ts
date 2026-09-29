import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:5178',
    },
  },
  build: {
    outDir: '../backend/src/PersonalCalendar.Api/wwwroot',
    emptyOutDir: true,
  },
})

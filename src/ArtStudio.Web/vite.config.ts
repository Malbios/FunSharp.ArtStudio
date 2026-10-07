import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const backendUrl = 'http://localhost:5180'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': backendUrl,
      '/hubs': { target: backendUrl, ws: true },
    },
  },
  build: {
    outDir: '../ArtStudio.Server/wwwroot',
    emptyOutDir: true,
  },
})

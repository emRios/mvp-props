import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/mvp-props': { 
        target: 'http://localhost:5002', 
        changeOrigin: true 
      },
      // Proxy de imágenes para evitar CORS en desarrollo
      '/mvp-props/images': {
        target: 'http://localhost:5002',
        changeOrigin: true,
        secure: false,
        // Mantener la ruta tal cual
        rewrite: (path) => path
      }
    }
  }
})
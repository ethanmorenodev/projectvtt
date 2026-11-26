import { defineConfig } from 'vitest/config'
import { loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// Backend local (dotnet run --project backend/Api). Sin tipos de Node en
// el proyecto: la meta es configurable con VITE_API_TARGET (por defecto el
// puerto estándar de desarrollo); las pruebas E2E la apuntan a su propia
// API (ADR-025).
const DEFAULT_API_TARGET = 'http://localhost:5000'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '')
  const apiTarget = env.VITE_API_TARGET || DEFAULT_API_TARGET
  // API REST + handshake de SignalR (el ws se reescribe a http).
  const proxy = {
    '/api': { target: apiTarget, changeOrigin: true },
    '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
  }

  return {
    plugins: [react()],
    server: { proxy },
    preview: { proxy },
    test: {
      environment: 'jsdom',
    },
  }
})

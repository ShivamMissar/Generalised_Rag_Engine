/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Forward API calls to the .NET API, so the page and the API look like one website to the browser.
    // That avoids CORS (the browser's block on calling a different address).
    proxy: {
      '/api': 'http://localhost:5209',
      '/health': 'http://localhost:5209',
    },
    // Allow importing the sample job ad from the repo's samples/ folder.
    fs: { allow: ['..'] },
  },
  test: {
    environment: 'node',
  },
})

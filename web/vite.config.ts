/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Where the .NET API runs. `npm run dev` (scripts/dev.mjs) starts it on this port too.
const apiUrl = `http://localhost:${process.env.RAG_API_PORT ?? '5209'}`

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Forward API calls to the .NET API, so the page and the API look like one website to the browser.
    // That avoids CORS (the browser's block on calling a different address).
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
    },
    // Allow importing the sample job ad from the repo's samples/ folder.
    fs: { allow: ['..'] },
  },
  test: {
    environment: 'node',
  },
})

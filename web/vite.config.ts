/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const api = 'http://localhost:5103';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': api,
      '/health': api,
      '/hubs': { target: api, ws: true },
    },
  },
  test: { environment: 'node' },
});

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Produkcyjny build trafia do wwwroot/ backendu (serwuje go ASP.NET Core).
// W trybie deweloperskim (npm run dev) Vite przekazuje /api do backendu na porcie 5178.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5178',
    },
  },
});

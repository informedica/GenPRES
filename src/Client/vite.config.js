import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import Inspect from "vite-plugin-inspect"

const proxyPort = process.env.SERVER_PROXY_PORT || "8085";
const proxyTarget = "http://localhost:" + proxyPort;
console.log("proxying to", proxyTarget);

// https://vite.dev/config/
export default defineConfig({
  base : "./",
  build: {
    outDir: "../../deploy/public",
    chunkSizeWarningLimit: 1000
  },
  server: {
    proxy: {
        // redirect requests that start with /api/ to the server on port 8085
        "/api": {
            target: proxyTarget,
            changeOrigin: true,
        }
    }
  },
  plugins: [
    Inspect(),
    react({ include: /\.(fs|js|jsx|ts|tsx)$/, jsxRuntime: "automatic" })
  ],
})


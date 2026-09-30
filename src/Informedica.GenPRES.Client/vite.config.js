import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import Inspect from "vite-plugin-inspect"

const proxyPort = process.env.SERVER_PROXY_PORT || "8085";
const proxyTarget = "http://localhost:" + proxyPort;
console.log("proxying to", proxyTarget);

// GENPRES_LOG from the environment or the repository .env; a debug build of the client turns its
// Elmish trace and debugger on only when the server logs too
const genpresLog = process.env.GENPRES_LOG ?? loadEnv("", "../..", "GENPRES_").GENPRES_LOG ?? "";

// https://vite.dev/config/
export default defineConfig({
  base : "./",
  define: {
    __GENPRES_LOG__: JSON.stringify(genpresLog)
  },
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
        },
        // the stub LaunchScript page (plan 605): served by the server in full scope only
        "/stub": {
            target: proxyTarget,
            changeOrigin: true,
        },
        // the identity hop (uc-01 step 4): the stub IdentityProvider and the callback
        "/authorize": {
            target: proxyTarget,
            changeOrigin: true,
        },
        "/callback": {
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


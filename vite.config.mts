import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const proxyPort = process.env.SERVER_PROXY_PORT || "8085";
const proxyTarget = "http://localhost:" + proxyPort;

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [react()],
//    build: {
//        outDir: "./deploy/public",
//    },
    root: "dist",
    server: {
        port: 5173,
        proxy: {
            // redirect requests that start with /api/ to the server on port 8085
            "/api": {
                target: proxyTarget,
                changeOrigin: true,
            }
        }
    }
});
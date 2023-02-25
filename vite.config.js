import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
    // under investigation: https://github.com/vitejs/vite/discussions/12200
    // root: 'src/Client',
    // build: {
    //     outDir : "../../dist"
    // },
    server: {
        proxy: {
            '/api' : {
                target: 'http://localhost:8085',
                changeOrigin: true
            }
        }
    },
    plugins: [react()]
})
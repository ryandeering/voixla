import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
    base: process.env.VITE_BASE ?? "/",
    plugins: [vue()],
    resolve: {
        alias: {
            "@": fileURLToPath(new URL("./src", import.meta.url)),
        },
    },
    server: {
        port: 5174,
        strictPort: true,
        proxy: {
            "/api": {
                target: "http://localhost:5005",
                changeOrigin: true,
            },
        },
    },
    build: {
        outDir: "../backend/wwwroot",
        emptyOutDir: true,
    },
});

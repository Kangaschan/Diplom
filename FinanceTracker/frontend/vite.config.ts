import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
    plugins: [react()],
    server: {
        port: 5173,
        allowedHosts: ["a769-67-213-208-39.ngrok-free.app"]
    }
});
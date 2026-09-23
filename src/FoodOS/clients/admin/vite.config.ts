import { defineConfig, loadEnv, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import fs from "node:fs";
import path from "node:path";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiBase = env.VITE_API_BASE_URL ?? "http://localhost:5030";

  // Local Vite is an explicit demo environment. The committed production
  // config stays fail-closed; Docker/Terraform opt in through runtime config.
  const devRuntimeConfig: Plugin = {
    name: "fsh-dev-runtime-config",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const url = req.url ?? "";
        if (url !== "/config.json" && !url.startsWith("/config.json?")) {
          next();
          return;
        }
        let base: Record<string, unknown> = {};
        try {
          base = JSON.parse(
            fs.readFileSync(path.resolve(__dirname, "public/config.json"), "utf8"),
          ) as Record<string, unknown>;
        } catch {
          // Fall back to runtime defaults if the file is missing/unreadable.
        }
        res.setHeader("Content-Type", "application/json");
        res.setHeader("Cache-Control", "no-store");
        res.end(JSON.stringify({ ...base, apiBase, demoMode: true }));
      });
    },
  };

  return {
    plugins: [devRuntimeConfig, react(), tailwindcss()],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        "/api": { target: apiBase, changeOrigin: true, secure: false },
        // /health is the SPA management page; only probe paths go to the API.
        "/health/": { target: apiBase, changeOrigin: true, secure: false },
        "/openapi": { target: apiBase, changeOrigin: true, secure: false },
        "/scalar": { target: apiBase, changeOrigin: true, secure: false },
      },
    },
  };
});

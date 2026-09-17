import { defineConfig } from "@playwright/test";
import base from "./playwright.config";

// Separate from the normal suite: both real frontends are required.
export default defineConfig({
  ...base,
  testDir: "./cross-app-tests",
  use: { ...base.use, baseURL: "http://localhost:5194" },
  webServer: [
    { command: "npm run dev -- --port 5194 --strictPort", url: "http://localhost:5194", reuseExistingServer: false },
    { command: 'npm --prefix "../dashboard" run dev -- --port 5195 --strictPort', url: "http://localhost:5195", reuseExistingServer: false },
  ],
});

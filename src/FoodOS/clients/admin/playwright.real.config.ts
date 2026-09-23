import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.PLAYWRIGHT_REAL_PORT ?? 5273);
const apiBase = process.env.PLAYWRIGHT_REAL_API_URL ?? "http://localhost:5030";

export default defineConfig({
  testDir: "./tests/real",
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL: `http://localhost:${port}`,
    locale: "en-US",
    trace: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: `npx vite --port ${port}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: false,
    timeout: 60_000,
    stdout: "ignore",
    stderr: "pipe",
    env: { ...process.env, VITE_API_BASE_URL: apiBase },
  },
});

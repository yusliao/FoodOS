import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/demo",
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL: process.env.PLAYWRIGHT_DEMO_ADMIN_URL ?? "http://localhost:19081",
    locale: "en-US",
    trace: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});

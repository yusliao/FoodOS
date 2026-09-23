import { defineConfig, devices } from "@playwright/test";
const port = Number(process.env.PLAYWRIGHT_PORT ?? 5174);

/**
 * Playwright config for the dashboard app.
 *
 * Tests run against a Vite dev server on port 5174 (the same port the
 * `dev` script uses), with API calls intercepted via `page.route()` so
 * tests don't need a running backend. This keeps the test loop fast
 * (~5s per test) and deterministic — no flaky network, no DB seeding.
 *
 * Real-backend tests live under `tests/real` and use the separate
 * `playwright.real.config.ts`; the default suite explicitly excludes them.
 *
 * Usage:
 *   npm run test:e2e               # headless, all browsers
 *   npm run test:e2e -- --ui       # interactive runner
 *   npm run test:e2e -- --headed   # see the browser drive
 *   npm run test:e2e -- auth.spec  # filter by file
 */
export default defineConfig({
  testDir: "./tests",
  testIgnore: ["**/real/**"],
  // Tests are deterministic (mocked APIs) so parallelism is safe and
  // dramatically faster. We still serialise within a file via test.serial
  // when state spans tests (e.g. password-reset multi-step flows).
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // Use all logical cores locally; throttle to 2 in CI to avoid
  // exhausting GitHub Actions runners.
  workers: process.env.CI ? 2 : undefined,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",

  use: {
    baseURL: `http://localhost:${port}`,
    locale: "en-US",
    trace: "on-first-retry",
    // Disable animations + reduce flake from CSS keyframes / transitions
    // (we have a lot — parallax orbs, fsh-enter staggers, btn-shimmer).
    // Tests assert against final state, not in-flight frames.
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
  },

  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],

  // Start an isolated server; PLAYWRIGHT_PORT keeps tests off a user's dev server.
  webServer: {
    command: `npm run dev -- --port ${port}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: false,
    timeout: 60_000,
    stdout: "ignore",
    stderr: "pipe",
  },
});

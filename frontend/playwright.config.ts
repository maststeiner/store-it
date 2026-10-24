import { defineConfig, devices } from '@playwright/test';

// E2E drives the real app: Playwright starts `ng serve` (which proxies /api to the
// backend on :5000, see proxy.conf.json). The backend + PostgreSQL are started
// separately (CI job / local: see docs/guidelines/test-guidelines.md).
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    // Pin the UI language so text selectors are deterministic (the app picks the
    // locale from navigator.language, falling back to English).
    locale: 'en-US',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
      testIgnore: /mobile-layout\.spec\.ts/,
    },
    {
      // SPEC-012 D8: phone emulation in Chromium (the only browser installed in CI job 1b).
      // Runs the layout spec only; the flows above stay desktop.
      name: 'mobile',
      testMatch: /mobile-layout\.spec\.ts/,
      use: {
        ...devices['iPhone 13'],
        browserName: 'chromium',
        viewport: { width: 390, height: 844 },
      },
    },
  ],
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});

import { defineConfig, devices } from '@playwright/test';

/**
 * E2E opcional — requiere stack levantado (docker compose o npm run dev + backend).
 * CI: job separado documentado en docs/testing-strategy.md.
 */
export default defineConfig({
    testDir: './e2e',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: process.env.CI ? 1 : undefined,
    timeout: process.env.CI ? 60_000 : 30_000,
    reporter: 'list',
    use: {
        baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000',
        trace: 'on-first-retry',
        actionTimeout: process.env.CI ? 20_000 : 10_000,
        navigationTimeout: process.env.CI ? 30_000 : 15_000,
    },
    projects: [
        { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    ],
});

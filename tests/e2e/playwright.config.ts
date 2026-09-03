import { defineConfig } from '@playwright/test';

// The app and its database are expected to be running already: starting them
// here would mean this config owning Docker, migrations and seed state.
// See docs/passkeys.md.
export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5000',
    trace: 'retain-on-failure',
  },
});

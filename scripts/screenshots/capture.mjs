// Captures every page in both themes, from the state seed.sql creates.
//
//   docker compose up -d
//   dotnet run                      # in another terminal, once, to migrate
//   psql "$CONNECTION" -f scripts/screenshots/seed.sql
//   npm --prefix scripts/screenshots run capture
//
// Re-apply seed.sql before every capture: the links it creates are single-use
// and a previous run will have spent them.
//
// Deterministic on purpose: fixed viewport, fixed data, animations disabled.
// A screenshot that changes when nothing changed is a screenshot nobody trusts.

import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const BASE_URL = process.env.SCREENSHOT_BASE_URL ?? 'http://localhost:5000';
const OUT_DIR = join(dirname(fileURLToPath(import.meta.url)), '../../docs/screenshots');

const VIEWPORT = { width: 1280, height: 900 };
const CREDENTIALS = { email: 'screenshot@example.com', password: 'screenshot-password' };

// Pages reachable without a session. The link pages take a token per theme:
// they are single-use, and verify-email redeems on load, so sharing one across
// both passes would photograph a dead link the second time.
const publicPages = (theme) => [
  { name: 'login', path: '/login' },
  { name: 'register', path: '/register' },
  { name: 'forgot-password', path: '/forgot-password' },
  { name: 'reset-password', path: `/reset-password?token=screenshot-reset-${theme}` },
  { name: 'verify-email', path: `/verify-email?token=screenshot-verify-${theme}` },
];

async function settle(page) {
  await page.waitForLoadState('networkidle');
  // The app fades nothing in, but a caret blinking in a focused field is enough
  // to make two runs differ.
  await page.addStyleTag({
    content: '*, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }',
  });
  await page.waitForTimeout(150);
}

async function capture(context, name, path, theme) {
  const page = await context.newPage();
  await page.goto(`${BASE_URL}${path}`, { waitUntil: 'domcontentloaded' });
  await settle(page);
  await page.screenshot({ path: join(OUT_DIR, `${name}-${theme}.png`) });
  await page.close();
  console.log(`  ${name}-${theme}.png`);
}

async function captureLanding(context, theme) {
  const page = await context.newPage();
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'domcontentloaded' });
  await page.fill('#email', CREDENTIALS.email);
  await page.fill('#password', CREDENTIALS.password);
  await page.click('button[type="submit"]');
  await page.waitForURL(`${BASE_URL}/`);
  await settle(page);
  await page.screenshot({ path: join(OUT_DIR, `landing-${theme}.png`) });
  await page.close();
  console.log(`  landing-${theme}.png`);
}

const browser = await chromium.launch();
await mkdir(OUT_DIR, { recursive: true });

try {
  for (const theme of ['light', 'dark']) {
    console.log(`${theme}:`);
    const context = await browser.newContext({ viewport: VIEWPORT, colorScheme: theme });
    try {
      for (const { name, path } of publicPages(theme)) {
        await capture(context, name, path, theme);
      }
      await captureLanding(context, theme);
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

console.log(`\nWrote ${(publicPages('light').length + 1) * 2} screenshots to docs/screenshots/`);

import { expect, test, type Page } from '@playwright/test';

/**
 * Passkeys cannot be exercised without a browser, and a real authenticator
 * cannot be scripted. Chrome's virtual authenticator, driven over CDP, performs
 * genuine WebAuthn ceremonies against real cryptography — the server verifies
 * these exactly as it would a phone.
 */
async function attachVirtualAuthenticator(page: Page) {
  const client = await page.context().newCDPSession(page);
  await client.send('WebAuthn.enable');
  const { authenticatorId } = await client.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      transport: 'internal',
      hasResidentKey: true,   // discoverable, so sign-in needs no email
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });
  return { client, authenticatorId };
}

const PASSWORD = 'correct-horse-battery';

// Every test registers its own account. Sharing one leaves passkeys behind
// from earlier runs, and the assertions then count somebody else's keys.
async function registerAccount(page: Page): Promise<string> {
  const email = `passkey-${crypto.randomUUID()}@example.com`;
  await page.goto('/register');
  await page.fill('#email', email);
  await page.fill('#password', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL('**/');
  return email;
}

test('a passkey can be added, then used to sign in without an email', async ({ page }) => {
  await attachVirtualAuthenticator(page);

  await registerAccount(page);

  // Add — the authenticator answers the prompt automatically.
  await page.getByRole('button', { name: 'Add a passkey' }).click();
  await expect(page.getByRole('listitem')).toHaveCount(1);

  // Sign out, so the next sign-in is real rather than a leftover session.
  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.waitForURL('**/login');

  // Sign in with no email typed at all: the whole point of a discoverable
  // credential is that the authenticator knows which account this is.
  await page.getByRole('button', { name: 'Sign in with a passkey' }).click();
  await page.waitForURL('**/');
  await expect(page.getByText("You're signed in.")).toBeVisible();
});

test('a removed passkey no longer signs anyone in', async ({ page }) => {
  await attachVirtualAuthenticator(page);

  await registerAccount(page);

  await page.getByRole('button', { name: 'Add a passkey' }).click();
  await expect(page.getByRole('listitem')).toHaveCount(1);

  await page.getByRole('button', { name: 'Remove' }).click();
  await expect(page.getByText('No passkeys yet.')).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.waitForURL('**/login');

  // The credential still exists in the authenticator; the server has forgotten
  // it, which is what must decide the outcome.
  await page.getByRole('button', { name: 'Sign in with a passkey' }).click();
  await expect(page).toHaveURL(/\/login/);
});

# Passkeys

Off by default. Enabled, a signed-in user can register a passkey and afterwards sign in with it instead of typing a password.

```jsonc
"Passkeys": {
  "Enabled": false,
  "RelyingPartyId": "localhost",              // a domain, never a URL, never a port
  "RelyingPartyName": "claude-starter",       // what the device shows when prompting
  "Origins": ["http://localhost:5000"]        // full origins allowed to complete a ceremony
}
```

Disabled, the routes are **not mapped** — absent rather than present-and-refusing — and `GET /api/config` reports `passkeysEnabled: false`, so the client never offers the option.

## What it does and does not change

The password stays the account's root. Reset, verification and recovery all keep working untouched, and losing every device is survivable. Passwordless registration is deliberately not offered: an account with no password has nothing to reset, and this template has no other recovery path.

Sign-in uses **discoverable credentials**, so the sign-in page asks for no email — the authenticator offers the accounts it holds. That requires resident keys, which every modern platform authenticator supports and some older USB keys do not.

`RelyingPartyId` binds credentials to a domain. **Changing it orphans every passkey already registered**, with no migration: they simply stop being offered. Set it to the registrable domain (`example.com`), not a host or a URL.

## Verification and sessions

A passkey sign-in respects `Auth:RequireEmailVerification` exactly as a password does, answering 403 with the same problem type. It issues the same claims too, security stamp included, so a password reset ends passkey sessions along with the rest.

The counter authenticators report is stored and updated on every use. A counter that fails to advance is how a cloned credential gives itself away.

## Where the challenge lives

Between issuing options and verifying the response, the challenge sits in a Data Protection-encrypted cookie that expires in five minutes and is deleted on read. No table, nothing to prune, and it works across instances because the keys are already shared through Postgres. Reading it consumes it, so one set of options completes one ceremony.

## Testing it

WebAuthn cannot be exercised without a browser, and a real authenticator cannot be scripted. `tests/e2e/` drives Chrome's **virtual authenticator** over CDP: genuine ceremonies against real cryptography, which the server verifies exactly as it would a phone.

```bash
docker compose up -d
Passkeys__Enabled=true dotnet run          # in another terminal

npm --prefix tests/e2e install
npx playwright install chromium
E2E_BASE_URL=http://localhost:5000 npm --prefix tests/e2e test
```

Two things these tests learned the hard way, both worth keeping in mind when adding more:

**Give every test its own account.** Passkeys persist in the database, so a shared account accumulates them and assertions start counting somebody else's keys.

**Raise the rate limit.** Each page load calls `/api/auth/me` inside the rate-limited `/api/auth` group, and a browser test loads plenty of pages.

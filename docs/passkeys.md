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

## Choosing a relying party id

`RelyingPartyId` decides which origins a passkey works on. It must either equal the origin's effective domain or be a registrable domain suffix of it, so an app served at `https://portal.example.com` may use either:

| Value | Valid | A passkey then works on |
| --- | --- | --- |
| `portal.example.com` | equals the effective domain | `portal.example.com` and anything below it |
| `example.com` | registrable domain suffix | every `*.example.com` — portal, api, admin |
| `m.portal.example.com` | no — not a suffix of the origin | |
| `com` | no — a public suffix | |

It is a scoping decision rather than a correctness one. Use the parent domain when subdomains are one product sharing accounts; use the host when they are separate apps, or when you would rather not have every subdomain able to exercise your users' credentials.

**Neither choice can be revised later.** Broadening `portal.example.com` to `example.com` orphans every passkey already registered, and narrowing does the same. There is no migration: they stop being offered, and each user enrols again.

Never a URL and never a port — those belong in `Origins`.

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

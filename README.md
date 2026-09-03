# claude-starter

[![CI](https://github.com/aamcatamney/claude-starter/actions/workflows/ci.yml/badge.svg)](https://github.com/aamcatamney/claude-starter/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 22](https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A .NET 10 application serving an Angular client from a single project, with cookie authentication, Dapper against PostgreSQL, and SQL migrations applied on startup. Optional SMTP unlocks email verification and password reset.

Everything here is meant to be edited. The palette is neutral so you can brand it, the auth is complete so you can build past it, and the decisions that would otherwise puzzle you are written down in [`docs/adr/`](docs/adr/).

## Stack

- .NET 10, one project (`claude-starter.csproj`)
- PostgreSQL, reached through Dapper + Npgsql in a repository pattern — no EF Core
- DbUp migrations, embedded from `Migrations/Scripts/*.sql`
- Cookie authentication, with Data Protection keys persisted in Postgres
- BCrypt.Net-Next for password hashing
- MailKit for SMTP — optional, off by default
- Angular 22 client in `ClientApp/`, served as static files
- Deployed as a container

## Getting started

You need the .NET 10 SDK, Docker, and Node 22.22.3+ or 24.15.0+ (Angular 22 sets that floor).

```bash
# 1. Postgres
docker compose up -d

# 2. Client dependencies
cd ClientApp && npm ci && cd ..

# 3. Build the client, then run the app — it serves the bundle too
cd ClientApp && npm run build && cd ..
dotnet run
```

The app creates the database if it is missing, then DbUp applies any pending scripts and records them in `schemaversions`. Re-running is idempotent.

There is no seed user. Register one at `/register` to get in.

### Dev loop

Two terminals, same origin — no `ng serve`, no proxy. The .NET host serves the Angular build output from `ClientApp/dist/claude-starter/browser`.

```bash
# Terminal 1 — rebuild the client into dist/ on every change
cd ClientApp
npm run watch

# Terminal 2 — the API, which also serves the client and the SPA fallback
dotnet run
```

`npm ci` installs exactly what `package-lock.json` records, which is what CI does. Reach for `npm install` only when you mean to change dependencies.

### Docker Compose

```bash
docker compose down      # stop, keep data
docker compose down -v   # stop and wipe the volume
```

## Project structure

```
Program.cs              Wiring: auth, rate limiting, options, SPA fallback
Endpoints/              Minimal API endpoints — one endpoint per file
  Auth/                 Everything under /api/auth
Repositories/           Dapper repositories
Services/
  Auth/                 Password hashing, email links, token cleanup
  Email/                SMTP and no-op senders, options
  DataProtection/       Postgres-backed key storage
Data/                   Npgsql connection factory, Dapper config
Models/                 Domain types
Migrations/
  DbMigrator.cs         DbUp runner, applied on startup
  Scripts/*.sql         Embedded, applied in name order
ClientApp/              Angular 22 client
tests/
  claude-starter.UnitTests/
  claude-starter.IntegrationTests/
docs/adr/               Decisions worth explaining
```

## API

Every route lives under `/api/auth` and is rate-limited.

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | — | Create an account |
| POST | `/api/auth/login` | — | Sign in; sets auth and `XSRF-TOKEN` cookies |
| POST | `/api/auth/logout` | required | Sign out |
| GET | `/api/auth/me` | required | The current user |
| POST | `/api/auth/forgot-password` | — | Send a reset link |
| POST | `/api/auth/reset-password` | — | Set a new password from a link |
| POST | `/api/auth/verify-email` | — | Confirm an address from a link |
| POST | `/api/auth/resend-verification` | — | Send another confirmation link |

Two behaviours are deliberate and easy to "fix" by accident:

- **`forgot-password` and `resend-verification` always answer 202**, whether or not the address exists. Anything else turns them into a way to test which addresses have accounts.
- **`logout` signs you out even if the antiforgery token is rejected.** Refusing would leave the cookie alive while the client cleared its own state — a signed-out screen in front of a live session. See [ADR 0003](docs/adr/0003-logout-is-not-gated-on-the-antiforgery-token.md).

## Configuration

| Setting | Default | What it does |
| --- | --- | --- |
| `ConnectionStrings:Postgres` | `localhost:5432`, `postgres`/`postgres` | Database |
| `Auth:RequireEmailVerification` | `false` | Refuse sign-in until the address is confirmed. Forced off while SMTP is disabled |
| `Auth:AppBaseUrl` | request origin | Origin used to build links in emails |
| `Auth:BCryptWorkFactor` | `12` | Password hashing cost. Raise as hardware improves |
| `Smtp:Enabled` | `false` | With this off, nothing is sent |
| `Smtp:Host` / `Port` / `UseStartTls` | — / `587` / `true` | Server |
| `Smtp:Username` / `Password` | — | Credentials, omitted for an open relay |
| `Smtp:FromAddress` / `FromName` | — | Sender |
| `RateLimit:Auth:PermitLimit` | `10` | Requests per window, per IP, across `/api/auth` |
| `RateLimit:Auth:WindowSeconds` | `60` | Length of that window |

Override any of them with environment variables, doubling the underscore for nesting:

```bash
ConnectionStrings__Postgres="Host=db;Port=5432;Database=claude_starter;Username=...;Password=..."
Smtp__Enabled=true
Auth__RequireEmailVerification=true
```

`Auth:BCryptWorkFactor` is worth understanding before you touch it: a hash at 12 costs a few hundred milliseconds by design, which is what makes guessing expensive. A unit test pins the default at 12 or above so it cannot quietly drop.

## Email verification and password reset

Both flows mail a single-use link. With `Smtp:Enabled` false nothing is sent — in Development the message is written to the log instead, so you can exercise the flows without a mail server. It is never logged in any other environment, because those bodies contain working links.

**`Auth:RequireEmailVerification` is ignored while SMTP is disabled**, whatever it is set to. Requiring a confirmation that nothing can send would leave every account — including yours — waiting forever with no way back in. See [ADR 0005](docs/adr/0005-email-links-are-hashed-single-use-and-cannot-outrun-smtp.md).

With verification required:

- Registering creates the account and sends a link but issues **no session**.
- Signing in before confirming returns **403**, not 401 — the credentials were right, the account is not ready — carrying a problem type the client uses to offer a resend.

Reset links last an hour, confirmation links 24 hours, and each works once; requesting a new one retires the old. Completing a reset ends every session opened before it. Spent and expired tokens are deleted by a background sweep, hourly and at startup, once they are 30 days past use — long enough to still answer "was a reset ever requested for this account?"

### Upgrading an existing deployment

Two things happen the first time this runs against a database that predates these features. Neither affects a fresh one.

**Everyone is signed out, once.** Cookies issued earlier carry no security-stamp claim and are rejected. Nothing is wrong; people sign in again.

**Existing users are `email_verified = false`**, because nobody has confirmed those addresses. That only bites if you then enable `Auth:RequireEmailVerification`, which would hold every existing account at the login gate. To grandfather them, decide deliberately and run:

```sql
UPDATE users SET email_verified = true;
```

## Tests

```bash
dotnet test              # backend; integration tests need Docker for Testcontainers

cd ClientApp
npm test -- --no-watch   # frontend
```

Integration tests start one Postgres container for the whole run. Each collection then creates its own database inside it, migrates it, and boots one application against it — which is what lets collections run in parallel. See [ADR 0004](docs/adr/0004-integration-tests-share-a-container-and-isolate-by-database.md).

Adding a test class means adding a collection: a `[CollectionDefinition]` and a matching `[Collection]` attribute. Forget the attribute and xunit reports *"constructor parameters did not have matching fixture data"*, which sounds like a dependency-injection problem and is not.

Tests hash at work factor 4. At the production 12, a suite that hashes on nearly every case spends all its time there.

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` with a zero-padded sequence number. It is picked up as an embedded resource automatically, and DbUp applies it in name order on the next startup.

## Continuous integration

Workflows run on a **self-hosted runner** labelled `self-hosted, linux, X64`.

`ci.yml` picks its runner per event. Pushes to `main` and pull requests from branches in this repository — all of which already require write access — use the self-hosted runner. **Pull requests from forks fall back to `ubuntu-latest`**: a fork PR is untrusted code, and `npm ci` and `dotnet test` would run it on your own machine, on a runner that persists between jobs. Keep that fallback while the repository is public.

The self-hosted machine needs:

- A .NET 10 SDK reachable by `actions/setup-dotnet` (linux-x64) and Node by `actions/setup-node`
- A running Docker daemon, for Testcontainers and for the release image
- Nothing else — the image targets `linux/amd64`, which is native here, so no QEMU is involved

`template-bootstrap.yml` targets the same runner, which only works while generated repositories can reach it — see [Using this template](#option-1--use-this-template-button-automatic).

To return to GitHub-hosted runners, set `runs-on: ubuntu-latest` across `.github/workflows/*.yml`.

## Versioning and releases

Versions are CalVer: **`YYYY.M.PATCH`** — `2026.9.0`, then `2026.9.1`. The patch counts releases within the month and restarts when the month turns over. The month is unpadded on purpose, which keeps the version valid semver and therefore sortable by tooling.

Every push to `main` touching anything other than Markdown or `LICENSE` starts a release, which:

1. Reads the existing `v*` tags, takes the highest patch for this month, and adds one. Nothing in the repository stores the version, so there is no bump commit and nothing to conflict over.
2. Builds the image, passing the version as a build arg that the Dockerfile forwards to `dotnet publish /p:Version=`. Local builds default to `0.0.0`.
3. Pushes to GHCR tagged `<version>`, `<year>.<month>`, `latest` and `sha-<short>`, with provenance attested.
4. Creates the git tag and a GitHub release, with generated notes and the image digest.

The tag comes last, so a failed build leaves none behind and the next run reuses the number.

Releases are serialised, and only one run may sit pending. Merging several pull requests in quick succession cancels queued runs that a newer merge overtakes, so those commits get no version of their own — they ship in the next release, which builds the head of `main`. Every commit reaches an image; not every commit gets a version number.

To cut a release without a merge — rebuilding against a new base image, say — run the workflow from the **Actions** tab.

## Frontend styling

`ClientApp/src/styles.css` holds the whole design layer, in two parts:

- **Tokens** — a Tailwind `@theme` block of semantic names (`--color-ink`, `--color-surface`, `--color-line`, `--radius-control`). The palette is deliberately neutral: one grey ramp, ink for emphasis, red only for danger. Brand a project by editing these values; nothing else names a colour. Dark mode is the same tokens redefined under `prefers-color-scheme`, so it costs no per-component work.
- **Component classes** — `.card`, `.input`, `.field-label`, `.btn` and friends in `@layer components`. Templates are plain HTML using those classes; there is no Angular component API to learn or maintain. See [ADR 0001](docs/adr/0001-neutral-token-layer-with-css-component-classes.md).

Do not change `@theme` to `@theme inline`. That inlines token values into the generated utilities, and every dark-mode override silently stops working.

## Decisions

Things a reader would otherwise have to reverse-engineer, and the trade-offs behind them:

| ADR | Decision |
| --- | --- |
| [0001](docs/adr/0001-neutral-token-layer-with-css-component-classes.md) | A neutral token layer with CSS component classes, rather than Angular components |
| [0002](docs/adr/0002-typescript-version-tracks-angular-peer-range.md) | TypeScript tracks Angular's build peer range, so its version looks perpetually behind |
| [0003](docs/adr/0003-logout-is-not-gated-on-the-antiforgery-token.md) | Logout signs you out even when the antiforgery token is rejected |
| [0004](docs/adr/0004-integration-tests-share-a-container-and-isolate-by-database.md) | Integration tests share one container and isolate by database |
| [0005](docs/adr/0005-email-links-are-hashed-single-use-and-cannot-outrun-smtp.md) | Email links are stored hashed and single-use, and verification cannot outrun SMTP |

## License

MIT — see [LICENSE](LICENSE).

<!-- TEMPLATE:START -->
## Using this template

### Option 1 — "Use this template" button (automatic)

Click **Use this template → Create a new repository**. A `Template bootstrap` GitHub Action runs on the new repo, derives the project name from the repository name (coerced to kebab-case), renames everything, and commits the result as `chore: bootstrap project from template`. Watch it under the **Actions** tab.

The workflow leaves itself in place (a GitHub token can't delete workflow files), but a sentinel check makes it a no-op on every later push. Delete `.github/workflows/template-bootstrap.yml` by hand once you're set up if you'd like it gone.

If your repository name can't be coerced to a valid kebab-case name (`^[a-z][a-z0-9-]{1,49}$`), the workflow fails with a clear error — rename the repo and re-run it from the Actions tab.

> **Runner requirement.** This workflow runs on `[self-hosted, linux, X64]`, like the rest of CI. The generated repository must have a runner with those labels available to it — an org-level runner shared with the new repo, or one registered against it directly. Without one the bootstrap job queues indefinitely and the rename never happens. If the new repo lives outside that runner's scope, change `runs-on` in `.github/workflows/template-bootstrap.yml` to `ubuntu-latest` (or run `./rename-project.sh` locally instead, Option 2 below).

### Option 2 — clone and rename locally

Clone the repo, then rename the project to your own name. The rename scripts replace every `claude-starter` / `claude_starter` placeholder in source + filenames, strip this section, delete `.git/`, clean `bin/` and `obj/`, and self-delete.

**Linux / macOS:**

```bash
./rename-project.sh my-new-app
```

**Windows (PowerShell):**

```powershell
./rename-project.ps1 my-new-app
```

Name must be kebab-case, 2-50 chars, starting with a letter (`^[a-z][a-z0-9-]{1,49}$`). The snake-case form (`my_new_app`) is derived automatically for namespaces and the Postgres database name.

Flags: `--yes` / `-y` skip the confirmation prompt; `--force` bypasses the safety guard that checks you are still in the template directory.

After it finishes:

```bash
git init && git add -A && git commit -m "Initial commit"
cd ClientApp && npm ci
dotnet restore my-new-app.sln
```
<!-- TEMPLATE:END -->

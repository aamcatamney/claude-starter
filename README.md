# claude-starter

.NET 10 web app serving an Angular client. Backend uses Dapper + Npgsql against PostgreSQL with SQL migrations applied on startup via DbUp.

## Stack

- .NET 10 (single project, `claude-starter.csproj`)
- PostgreSQL
- Dapper + Npgsql (repository pattern, no EF Core)
- DbUp for migrations (embedded `Migrations/Scripts/*.sql`)
- BCrypt.Net-Next for password hashing
- Cookie authentication (ASP.NET Core), Data Protection keys persisted in Postgres
- Angular client in `ClientApp/`, served as static files

## Prerequisites

- .NET 10 SDK
- Docker (or a local Postgres instance)
- Node.js + npm (for the Angular client)

## Configuration

Connection string is read from `ConnectionStrings:Postgres`. Default in `appsettings.json` points at `localhost:5432` as `postgres` / `postgres`. Override via env var in containers:

```
ConnectionStrings__Postgres=Host=db;Port=5432;Database=claude_starter;Username=...;Password=...
```

Auth rate limit is configurable via `RateLimit:Auth:PermitLimit` (default `10`) and `RateLimit:Auth:WindowSeconds` (default `60`). Integration tests bump the limit so they don't trip on shared loopback IPs.

## Running locally

Start Postgres via Docker Compose:

```powershell
docker compose up -d
```

Tear down (keep data):

```powershell
docker compose down
```

Tear down and wipe the volume:

```powershell
docker compose down -v
```

Install Angular dependencies (first run):

```powershell
cd ClientApp
npm install
cd ..
```

### Dev loop

Two terminals, both same-origin (Angular output served from `wwwroot` by .NET — no `ng serve`, no proxy):

```powershell
# Terminal 1 — rebuild Angular on change into dist/
cd ClientApp
npm run watch

# Terminal 2 — run API (also serves the Angular bundle and SPA fallback)
dotnet run
```

### Production build

```powershell
cd ClientApp
npm run build
cd ..
dotnet run
```

On startup the app ensures the database exists, then DbUp applies any pending scripts from `Migrations/Scripts/` and tracks them in the `schemaversions` table. Re-running is idempotent.

### Frontend tests

```powershell
cd ClientApp
npm test
```

Vitest (jsdom) via the Angular `@angular/build:unit-test` builder. Specs live next to the code under `src/app/core/auth/*.spec.ts`.

## Container build

Multi-stage `Dockerfile` builds the Angular client (`node:22-bookworm-slim`), publishes the .NET app (`dotnet/sdk:10.0`), then ships on `dotnet/aspnet:10.0`. Runs as the image's non-root `$APP_UID` user on port `8080`.

```powershell
docker build -t claude-starter .
docker run --rm -p 8080:8080 `
  -e ConnectionStrings__Postgres="Host=host.docker.internal;Port=5432;Database=claude_starter;Username=postgres;Password=postgres" `
  claude-starter
```

The app applies migrations on startup, so the database must be reachable when the container starts. Sequence the dependency at the orchestrator level (`depends_on: condition: service_healthy` in compose, init container in k8s).

## Backend tests

Two test projects under `tests/`, both xUnit v3 with AwesomeAssertions and NSubstitute:

| Project                              | Scope                                                    | Docker required |
|--------------------------------------|----------------------------------------------------------|-----------------|
| `tests/claude-starter.UnitTests`     | Pure services (`BCryptPasswordHasher`)                   | No              |
| `tests/claude-starter.IntegrationTests` | `DbMigrator`, `UserRepository`, all `/api/auth/*` endpoints | Yes (Testcontainers spins up `postgres:16`) |

### Run

```powershell
# Just the unit tests (fast, no Docker)
dotnet test --project tests/claude-starter.UnitTests

# Just the integration tests (needs Docker running)
dotnet test --project tests/claude-starter.IntegrationTests

# Everything
dotnet test
```

### Integration test design

- **One Postgres container per test run**, shared across the assembly via xUnit `ICollectionFixture<PostgresFixture>`.
- **Migrations run once** at fixture start via `DbMigrator.Apply`.
- **State reset between tests** via [Respawn](https://github.com/jbogard/Respawn). `schemaversions` and `data_protection_keys` are excluded so migration tracking and the in-memory key cache survive.
- **App boot** uses `WebApplicationFactory<Program>`. The factory injects the container's connection string via `UseSetting("ConnectionStrings:Postgres", ...)` and runs in `Development` so cookie security policy permits HTTP loopback.
- **Pre-existing users** are created through `TestDataSeeder`, which hashes via the real `BCryptPasswordHasher` so login tests can verify against them.

## Database schema

### `users`

| Column          | Type          | Notes                                  |
|-----------------|---------------|----------------------------------------|
| `id`            | `uuid`        | PK, defaults to `gen_random_uuid()`    |
| `email`         | `text`        | Stored lower-cased by repository       |
| `password_hash` | `text`        | BCrypt hash (work factor 12)           |
| `display_name`  | `text` null   |                                        |
| `is_active`     | `boolean`     | Defaults to `true`                     |
| `created_at`    | `timestamptz` | Defaults to `now()`                    |
| `updated_at`    | `timestamptz` | Defaults to `now()`; bumped on update  |

Unique index `ux_users_email_lower` on `lower(email)` enforces case-insensitive uniqueness.

### `data_protection_keys`

Persists ASP.NET Core Data Protection keys so cookies survive container restarts and rolling deploys. Managed by a custom `IXmlRepository` over Dapper — never edit by hand.

| Column          | Type          | Notes                              |
|-----------------|---------------|------------------------------------|
| `id`            | `serial`      | PK                                 |
| `friendly_name` | `text` null   | Set by Data Protection             |
| `xml`           | `text`        | Serialized key XML                 |
| `created_at`    | `timestamptz` | Defaults to `now()`                |

## Authentication

Cookie auth via `Microsoft.AspNetCore.Authentication.Cookies`. No Identity, no JWT. Cookies are HttpOnly, SameSite=Lax, Secure in non-Development, 14-day sliding expiration. Session state is checked live: if `users.is_active` flips to `false`, the next request rejects the principal and signs out.

CSRF is mitigated by SameSite=Lax plus ASP.NET Core antiforgery. Login/register issue an `XSRF-TOKEN` cookie (non-HttpOnly) that Angular's `HttpClientXsrfModule` reads and replays as `X-XSRF-TOKEN`. `/me` re-issues the token so SPA reloads stay armed. Logout enforces the antiforgery token; login and register are exempt (no auth context yet).

Rate limit: 10 requests/minute per remote IP across `/api/auth/*` (fixed window).

### Endpoints

All routes are under `/api/auth`. Each endpoint lives in its own file under `Endpoints/Auth/`.

| Method | Path        | Auth     | Body                                              | Success                                  |
|--------|-------------|----------|---------------------------------------------------|------------------------------------------|
| POST   | `/login`    | -        | `{ email, password, rememberMe }`                 | 200 `{ id, email, displayName }`         |
| POST   | `/register` | -        | `{ email, password, displayName? }`               | 200 `{ id, email, displayName }`         |
| GET    | `/me`       | required | -                                                 | 200 `{ id, email, displayName }`         |
| POST   | `/logout`   | required | XSRF header                                       | 204                                      |

Failures use RFC 7807 `application/problem+json`:

| Status | Cause                                                    |
|--------|----------------------------------------------------------|
| 400    | Validation (bad email, password < 12 chars, bad XSRF)    |
| 401    | Invalid credentials, missing/expired cookie, inactive    |
| 409    | Email already registered                                 |
| 429    | Rate limit exceeded                                      |

### Smoke test

Run the API, then exercise the flow with a cookie jar:

```powershell
$base = "http://localhost:5000"

# Register (auto signs in)
curl.exe -c cookies.txt -b cookies.txt -X POST "$base/api/auth/register" `
  -H "Content-Type: application/json" `
  -d '{"email":"smoke@example.com","password":"correct-horse-battery","displayName":"Smoke"}'

# Whoami (also re-issues XSRF-TOKEN)
curl.exe -c cookies.txt -b cookies.txt "$base/api/auth/me"

# Logout (needs XSRF header — read it from cookie jar)
$xsrf = (Select-String -Path cookies.txt -Pattern 'XSRF-TOKEN\s+(\S+)$').Matches[0].Groups[1].Value
curl.exe -c cookies.txt -b cookies.txt -X POST "$base/api/auth/logout" -H "X-XSRF-TOKEN: $xsrf"

# Login again
curl.exe -c cookies.txt -b cookies.txt -X POST "$base/api/auth/login" `
  -H "Content-Type: application/json" `
  -d '{"email":"smoke@example.com","password":"correct-horse-battery","rememberMe":true}'
```

## Frontend

Angular 21 (zoneless-ready, standalone components, signals). NgRx Signal Stores for shared state. Tailwind v4 for styling.

### Routes

| Path        | Auth          | Component           | Notes                                   |
|-------------|---------------|---------------------|-----------------------------------------|
| `/`         | required      | `landing.page.ts`   | Placeholder home with sign-out          |
| `/login`    | anonymous     | `login.page.ts`     | Authed user is redirected back          |
| `/register` | anonymous     | `register.page.ts`  | Authed user is redirected back          |
| `/**`       | -             | -                   | Falls through to `/`                    |

`authGuard` and `redirectIfAuthedGuard` both read the root-provided `AuthStore`. Unauthed access to `/` redirects to `/login?returnUrl=/`. Both auth pages honour `returnUrl` on success (relative URLs only — open-redirect protection rejects absolute and protocol-relative values).

### Bootstrap

`provideAppInitializer` calls `GET /api/auth/me` before the first route resolves, hydrating `AuthStore.user` and `status`. The same call re-issues the `XSRF-TOKEN` cookie so logout has a token ready.

`provideHttpClient(withFetch(), withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }))` matches the backend's antiforgery configuration. Same-origin only — that's why `npm run watch` + `dotnet run` is the dev loop (no proxy needed).

### `AuthStore`

Root-provided NgRx Signal Store (`@ngrx/signals`) with state `{ user, status, error, pending }` and methods `loadMe / login / register / logout / clearError`. `displayName` falls back to email when `displayName` is null.

### Error handling

`auth-error.ts` maps RFC 7807 `problem+json` responses to a small `AuthErrorKind` union (`invalid-credentials`, `email-taken`, `validation`, `rate-limited`, `network`, `unknown`). The store stores one error at a time; the form pages render it as an inline `role="alert"` banner.

A global `authInterceptor` catches 401s on non-`/api/auth/*` calls, clears the store, and routes to `/login` with `returnUrl` set to the current URL.

## Project layout

```
Data/                                       IDbConnectionFactory + Npgsql impl
Migrations/                                 DbMigrator + Scripts/*.sql (embedded)
Models/                                     Domain models
Repositories/                               Dapper repositories
Services/Auth/                              IPasswordHasher + BCrypt impl
Services/DataProtection/                    PostgresXmlRepository (Data Protection keys)
Endpoints/Auth/                             Minimal API endpoints (one per file)
tests/claude-starter.UnitTests/             xUnit v3 unit tests
tests/claude-starter.IntegrationTests/      xUnit v3 + Testcontainers integration tests
ClientApp/src/app/
  core/auth/                                AuthStore, api, guards, interceptor, error mapper
  features/auth/                            login + register pages, lazy auth.routes.ts
  features/landing/                         placeholder home with top bar + sign out
Program.cs                                  Composition root, runs migrations on startup
```

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

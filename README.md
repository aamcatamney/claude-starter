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

## Running locally

Start a Postgres container:

```powershell
docker run --rm -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

Build the Angular client (first run):

```powershell
cd ClientApp
npm install
npm run build
cd ..
```

Run the API:

```powershell
dotnet run
```

On startup the app ensures the database exists, then DbUp applies any pending scripts from `Migrations/Scripts/` and tracks them in the `schemaversions` table. Re-running is idempotent.

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

## Project layout

```
Data/                       IDbConnectionFactory + Npgsql impl
Migrations/                 DbMigrator + Scripts/*.sql (embedded)
Models/                     Domain models
Repositories/               Dapper repositories
Services/Auth/              IPasswordHasher + BCrypt impl
Services/DataProtection/    PostgresXmlRepository (Data Protection keys)
Endpoints/Auth/             Minimal API endpoints (one per file)
ClientApp/                  Angular app
Program.cs                  Composition root, runs migrations on startup
```

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

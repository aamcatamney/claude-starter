# claude-starter

[![CI](https://github.com/aamcatamney/claude-starter/actions/workflows/ci.yml/badge.svg)](https://github.com/aamcatamney/claude-starter/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 21](https://img.shields.io/badge/Angular-21-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

.NET 10 web app serving an Angular client. Backend uses Dapper + Npgsql against PostgreSQL with SQL migrations applied on startup via DbUp.

## Stack

- .NET 10 (single project, `claude-starter.csproj`)
- PostgreSQL
- Dapper + Npgsql (repository pattern, no EF Core)
- DbUp for migrations (embedded `Migrations/Scripts/*.sql`)
- BCrypt.Net-Next for password hashing
- Cookie authentication (ASP.NET Core), Data Protection keys persisted in Postgres
- Angular 21 client in `ClientApp/`, served as static files

## Project structure

```
Program.cs              App wiring, auth, rate limiting, SPA fallback
Endpoints/              Minimal API endpoints — one endpoint per file
  Auth/                 /api/auth login, logout, register, me
Repositories/           Dapper repositories (no EF Core)
Services/               BCrypt password hashing, Postgres-backed Data Protection
Data/                   Npgsql connection factory + Dapper config
Models/                 Domain types
Migrations/
  DbMigrator.cs         DbUp runner (applied on startup)
  Scripts/*.sql         Embedded migration scripts, applied in name order
ClientApp/              Angular 21 client
```

## API

All routes are rate-limited and live under `/api/auth`:

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | — | Create a user |
| POST | `/api/auth/login` | — | Sign in, sets auth + `XSRF-TOKEN` cookies |
| POST | `/api/auth/logout` | required | Sign out |
| GET | `/api/auth/me` | required | Current user |

There is no seed user — register one to get started.

## Prerequisites

- .NET 10 SDK
- Docker (or a local Postgres instance)
- Node.js + npm (for the Angular client)

## Configuration

Connection string is read from `ConnectionStrings:Postgres`. Default in `appsettings.json` points at `localhost:5432` as `postgres` / `postgres`. Override via env var:

```
ConnectionStrings__Postgres=Host=db;Port=5432;Database=claude_starter;Username=...;Password=...
```

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

On startup the app ensures the database exists, then DbUp applies any pending scripts from `Migrations/Scripts/` and tracks them in the `schemaversions` table. Re-running is idempotent.

### Production build

```powershell
cd ClientApp
npm run build
cd ..
dotnet run
```

### Tests

```powershell
# Backend (integration tests need Docker running for Testcontainers)
dotnet test

# Frontend
cd ClientApp
npm test
```

## Continuous integration

All workflows run on a **self-hosted runner** labelled `self-hosted, linux, ARM64`. Requirements on that machine:

- .NET 10 SDK toolchain fetchable by `actions/setup-dotnet` (linux-arm64) and Node 22 by `actions/setup-node`
- A running Docker daemon — integration tests use Testcontainers, and the release workflow builds the image
- `binfmt`/QEMU available to Docker: the release image targets `linux/amd64`, so `docker/setup-qemu-action` registers emulation. Emulated builds are considerably slower than native ones.

`template-bootstrap.yml` targets the same runner, which only works while generated repos can reach it — see [Using this template](#option-1--use-this-template-button-automatic).

To go back to GitHub-hosted runners, set `runs-on: ubuntu-latest` in `.github/workflows/*.yml` and drop the QEMU step from `release.yml`.

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

## License

MIT — see [LICENSE](LICENSE).

<!-- TEMPLATE:START -->
## Using this template

### Option 1 — "Use this template" button (automatic)

Click **Use this template → Create a new repository**. A `Template bootstrap` GitHub Action runs on the new repo, derives the project name from the repository name (coerced to kebab-case), renames everything, and commits the result as `chore: bootstrap project from template`. Watch it under the **Actions** tab.

The workflow leaves itself in place (a GitHub token can't delete workflow files), but a sentinel check makes it a no-op on every later push. Delete `.github/workflows/template-bootstrap.yml` by hand once you're set up if you'd like it gone.

If your repository name can't be coerced to a valid kebab-case name (`^[a-z][a-z0-9-]{1,49}$`), the workflow fails with a clear error — rename the repo and re-run it from the Actions tab.

> **Runner requirement.** This workflow runs on `[self-hosted, linux, ARM64]`, like the rest of CI. The generated repository must have a runner with those labels available to it — an org-level runner shared with the new repo, or one registered against it directly. Without one the bootstrap job queues indefinitely and the rename never happens. If the new repo lives outside that runner's scope, change `runs-on` in `.github/workflows/template-bootstrap.yml` to `ubuntu-latest` (or run `./rename-project.sh` locally instead, Option 2 below).

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
cd ClientApp && npm install
dotnet restore my-new-app.sln
```
<!-- TEMPLATE:END -->

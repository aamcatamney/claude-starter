# claude-starter

.NET 10 web app serving an Angular client. Backend uses Dapper + Npgsql against PostgreSQL with SQL migrations applied on startup via DbUp.

## Stack

- .NET 10 (single project, `claude-starter.csproj`)
- PostgreSQL
- Dapper + Npgsql (repository pattern, no EF Core)
- DbUp for migrations (embedded `Migrations/Scripts/*.sql`)
- BCrypt.Net-Next for password hashing
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

## Project layout

```
Data/             IDbConnectionFactory + Npgsql impl
Migrations/       DbMigrator + Scripts/*.sql (embedded)
Models/           Domain models
Repositories/     Dapper repositories
Services/Auth/    IPasswordHasher + BCrypt impl
ClientApp/        Angular app
Program.cs        Composition root, runs migrations on startup
```

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

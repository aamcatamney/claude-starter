# Architecture

- Backend to use Dotnet 10 as a single project
- Do not use swagger
- Do not use Entity Framework Core
- Use Dapper in a repository pattern with Npgsql
- Use postgresql as the database
- Use a migration scripts folder with a service that applies them on startup
- Deployment will be done via a container
- Use Minimal API endpoints, one endpoint per file under `Endpoints/`
- Keep the README.md updated

# Skills

Always, in every session:

- `caveman` — respond in caveman mode. Stays on for the whole session, not just the first reply.
- `grill-with-docs` — interview me about a plan before building it. One question at a time, each with your recommended answer. Explore the codebase instead of asking anything the code can answer. Keep `CONTEXT.md` and `docs/adr/` current as decisions land.

When the work touches the frontend or UI — anything under `ClientApp/`, styling, components, templates, routing, or client state:

- `frontend-design` — for visual direction, layout, typography and copy.
- `angular-developer` — for Angular APIs, patterns and CLI usage. Run `ng build` before claiming the work is done.

# Screenshots

Any page you add, or any page whose appearance you change, gets re-captured before the work is done. The images in `docs/screenshots/` are the only thing in a pull request that shows whether a change works in the dark theme.

Always capture through `scripts/screenshots/`, never by hand — the fixed viewport and seeded data are what make two runs comparable. Apply `scripts/screenshots/seed.sql` first, every time: its links are single-use and a previous run will have spent them. Run the app with `RateLimit__Auth__PermitLimit=10000` while capturing, or the sign-in for the landing shot is rate-limited.

```bash
psql "$CONNECTION_STRING" -f scripts/screenshots/seed.sql
npm --prefix scripts/screenshots run capture
```

Both themes are captured for every page. Commit the PNGs alongside the change that caused them. Full detail in [docs/screenshots.md](docs/screenshots.md).

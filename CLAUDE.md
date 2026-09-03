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

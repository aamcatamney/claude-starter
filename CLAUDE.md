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

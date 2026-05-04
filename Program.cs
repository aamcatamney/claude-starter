using Dapper;
using Microsoft.Extensions.FileProviders;
using claude_starter.Data;
using claude_starter.Migrations;
using claude_starter.Repositories;
using claude_starter.Services.Auth;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");

DbMigrator.Apply(connectionString);

var app = builder.Build();

// Serve static files from Angular build output
var clientAppPath = Path.Combine(builder.Environment.ContentRootPath, "ClientApp", "dist", "claude-starter", "browser");
if (Directory.Exists(clientAppPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientAppPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name == "ngsw-worker.js" || ctx.File.Name == "ngsw.json")
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache";
            }
        }
    });

    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientAppPath)
    });
}

app.Run();

using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
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

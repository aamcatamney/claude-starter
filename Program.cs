using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using claude_starter.Data;
using claude_starter.Endpoints.Auth;
using claude_starter.Migrations;
using claude_starter.Repositories;
using claude_starter.Services.Auth;
using claude_starter.Services.DataProtection;
using claude_starter.Services.Diagnostics;
using claude_starter.Services.Email;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserTokenRepository, UserTokenRepository>();
builder.Services.AddScoped<EmailLinkService>();
builder.Services.AddHostedService<TokenCleanupService>();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));

var smtpEnabled = builder.Configuration.GetValue<bool>($"{SmtpOptions.SectionName}:Enabled");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.PostConfigure<AuthOptions>(options =>
{
    // Requiring a verification that cannot be sent would lock every account
    // out permanently, so SMTP being off wins over the setting being on.
    if (!smtpEnabled)
    {
        options.RequireEmailVerification = false;
    }
});

if (smtpEnabled)
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
}

builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.AddSingleton<AppMetrics>();

var metrics = builder.Configuration.GetSection(MetricsOptions.SectionName).Get<MetricsOptions>()
    ?? new MetricsOptions();

if (metrics.Enabled)
{
    // Nothing is exposed over HTTP: measurements are pushed to a collector,
    // so there is no scrape endpoint to leave unprotected.
    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(metrics.ServiceName))
        .WithMetrics(meters => meters
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(AppMetrics.MeterName)
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(metrics.OtlpEndpoint)));
}

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");

DbMigrator.Apply(connectionString);

builder.Services
    .AddDataProtection()
    .SetApplicationName("claude-starter");

builder.Services.AddSingleton<PostgresXmlRepository>();
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(o =>
        o.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>()));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async ctx =>
        {
            var idClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var id))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(id, ctx.HttpContext.RequestAborted);
            if (user is null || !user.IsActive)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            // A password reset rotates the stamp, so cookies minted before it
            // no longer match and stop being accepted here.
            var stamp = ctx.Principal?.FindFirstValue(AuthEndpoints.SecurityStampClaim);
            if (!Guid.TryParse(stamp, out var presented) || presented != user.SecurityStamp)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = ".AspNetCore.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimit:Auth:PermitLimit") ?? 10;
var authWindowSeconds = builder.Configuration.GetValue<int?>("RateLimit:Auth:WindowSeconds") ?? 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

if (Directory.Exists(clientAppPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientAppPath)
    });
}

app.Run();

public partial class Program;

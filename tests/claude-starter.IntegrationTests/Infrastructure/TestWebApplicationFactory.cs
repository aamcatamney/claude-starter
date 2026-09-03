using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using claude_starter.Services.Email;

namespace claude_starter.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IReadOnlyDictionary<string, string?> _settings;

    /// <summary>
    /// Nothing is ever sent over SMTP in tests; this captures what would have
    /// been, and is where a test reads the token out of a link.
    /// </summary>
    public CapturingEmailSender Emails { get; } = new();

    public TestWebApplicationFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        _connectionString = connectionString;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseSetting("RateLimit:Auth:PermitLimit", "10000");
        // Production hashes at work factor 12, which costs ~0.4s per call. The
        // suite hashes constantly and is not testing the cost function.
        builder.UseSetting("Auth:BCryptWorkFactor", TestDataSeeder.WorkFactor.ToString());
        builder.UseEnvironment("Development");

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

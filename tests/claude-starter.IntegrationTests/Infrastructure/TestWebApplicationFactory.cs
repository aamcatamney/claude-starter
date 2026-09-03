using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace claude_starter.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseSetting("RateLimit:Auth:PermitLimit", "10000");
        // Production hashes at work factor 12, which costs ~0.4s per call. The
        // suite hashes constantly and is not testing the cost function.
        builder.UseSetting("Auth:BCryptWorkFactor", TestDataSeeder.WorkFactor.ToString());
        builder.UseEnvironment("Development");
    }
}

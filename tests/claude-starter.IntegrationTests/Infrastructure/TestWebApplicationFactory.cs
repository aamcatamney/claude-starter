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
        builder.UseEnvironment("Development");
    }
}

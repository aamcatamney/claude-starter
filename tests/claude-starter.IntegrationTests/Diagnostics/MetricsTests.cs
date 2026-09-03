using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using claude_starter.IntegrationTests.Infrastructure;
using claude_starter.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace claude_starter.IntegrationTests.Diagnostics;

/// <summary>
/// Records what an instrument reports, so a test can assert on measurements
/// without an exporter or a collector in the way.
/// </summary>
internal sealed class CounterRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(string Instrument, long Value, string? Outcome, string? Purpose)> _measurements = [];
    private readonly object _gate = new();

    public CounterRecorder(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? outcome = null, purpose = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome") outcome = tag.Value?.ToString();
                if (tag.Key == "purpose") purpose = tag.Value?.ToString();
            }

            lock (_gate)
            {
                _measurements.Add((instrument.Name, value, outcome, purpose));
            }
        });

        _listener.Start();
    }

    public long Total(string instrument, string? outcome = null, string? purpose = null)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => m.Instrument == instrument)
                .Where(m => outcome is null || m.Outcome == outcome)
                .Where(m => purpose is null || m.Purpose == purpose)
                .Sum(m => m.Value);
        }
    }

    public void Dispose() => _listener.Dispose();
}

[Collection(MetricsCollection.Name)]
public sealed class MetricsTests : IntegrationTestBase
{
    public MetricsTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SignIn_RecordsOutcomes()
    {
        await Seeder.CreateUserAsync("counted@example.com", "correct-horse-battery");
        using var recorder = new CounterRecorder(AppMetrics.MeterName);

        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "counted@example.com",
            password = "wrong-password-entirely",
            rememberMe = false,
        });
        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "counted@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });

        recorder.Total("auth.sign_in", outcome: "success").Should().Be(1);
        recorder.Total("auth.sign_in", outcome: "invalid-credentials").Should().Be(1);
    }

    [Fact]
    public async Task Registration_RecordsOutcomes()
    {
        using var recorder = new CounterRecorder(AppMetrics.MeterName);

        var payload = new { email = "metered@example.com", password = "correct-horse-battery", displayName = (string?)null };
        (await Client.PostAsJsonAsync("/api/auth/register", payload)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync("/api/auth/register", payload)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        recorder.Total("auth.registration", outcome: "created").Should().Be(1);
        recorder.Total("auth.registration", outcome: "duplicate").Should().Be(1);
    }

    [Fact]
    public async Task TokenRedemption_RecordsRejections()
    {
        using var recorder = new CounterRecorder(AppMetrics.MeterName);

        var response = await Client.PostAsJsonAsync("/api/auth/verify-email", new { token = "not-a-real-token" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        recorder.Total("auth.token_redemption", outcome: "rejected", purpose: "email_verification")
            .Should().Be(1);
    }

    // Off by default is the whole point: no exporter, no collection, nothing
    // reaching out to a collector that may not exist.
    [Fact]
    public void MetricsAreOffByDefault()
    {
        using var scope = Fixture.Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<MeterProvider>().Should().BeNull();
        // The instruments still exist, so endpoints need no conditional code.
        scope.ServiceProvider.GetService<AppMetrics>().Should().NotBeNull();
    }
}

[Collection(MetricsEnabledCollection.Name)]
public sealed class MetricsEnabledTests
{
    private readonly MetricsEnabledFixture _fixture;

    public MetricsEnabledTests(MetricsEnabledFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void EnablingMetrics_RegistersACollector()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<MeterProvider>().Should().NotBeNull();
    }

    // Metrics are pushed, not scraped: there is no endpoint to expose, which is
    // why none needs protecting. /metrics is not 404 — the SPA fallback answers
    // any unmatched non-API path — so the assertion is that what comes back is
    // the client, never an exposition format.
    [Fact]
    public async Task NoScrapeEndpointIsPublished()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("# HELP");
        body.Should().NotContain("auth_sign_in");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/plain");
    }
}

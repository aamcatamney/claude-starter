namespace claude_starter.IntegrationTests.Infrastructure;

/// <summary>
/// SMTP on and verification required — the posture where registration does not
/// sign anyone in and unverified logins are refused.
/// </summary>
public sealed class VerificationRequiredFixture : DatabaseFixture
{
    public VerificationRequiredFixture(ContainerFixture container) : base(container) { }

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?>
        {
            ["Auth:AllowPublicRegistration"] = "true",
            ["Smtp:Enabled"] = "true",
            ["Auth:RequireEmailVerification"] = "true",
        };
}

/// <summary>
/// Verification asked for but SMTP off. The application is expected to ignore
/// the request rather than lock everyone out.
/// </summary>
public sealed class VerificationWithoutSmtpFixture : DatabaseFixture
{
    public VerificationWithoutSmtpFixture(ContainerFixture container) : base(container) { }

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?>
        {
            ["Auth:AllowPublicRegistration"] = "true",
            ["Smtp:Enabled"] = "false",
            ["Auth:RequireEmailVerification"] = "true",
        };
}

/// <summary>Metrics switched on, pointed at a collector that need not exist.</summary>
public sealed class MetricsEnabledFixture : DatabaseFixture
{
    public MetricsEnabledFixture(ContainerFixture container) : base(container) { }

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?>
        {
            ["Auth:AllowPublicRegistration"] = "true",
            ["Metrics:Enabled"] = "true",
            ["Metrics:OtlpEndpoint"] = "http://localhost:4317",
        };
}

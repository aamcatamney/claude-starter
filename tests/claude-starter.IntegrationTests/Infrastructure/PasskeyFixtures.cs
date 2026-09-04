namespace claude_starter.IntegrationTests.Infrastructure;

/// <summary>Passkeys switched on, bound to the test host's origin.</summary>
public sealed class PasskeysEnabledFixture : DatabaseFixture
{
    public PasskeysEnabledFixture(ContainerFixture container) : base(container) { }

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?>
        {
            ["Auth:AllowPublicRegistration"] = "true",
            ["Passkeys:Enabled"] = "true",
            ["Passkeys:RelyingPartyId"] = "localhost",
            ["Passkeys:Origins:0"] = "http://localhost",
        };
}

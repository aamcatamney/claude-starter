namespace claude_starter.IntegrationTests.Infrastructure;

/// <summary>
/// The production posture: nobody may register without an invite.
/// </summary>
public sealed class ClosedRegistrationFixture : DatabaseFixture
{
    public ClosedRegistrationFixture(ContainerFixture container) : base(container) { }

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?>
        {
            ["Auth:AllowPublicRegistration"] = "false",
        };
}

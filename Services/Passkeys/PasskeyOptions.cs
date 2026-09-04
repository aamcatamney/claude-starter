namespace claude_starter.Services.Passkeys;

public sealed class PasskeyOptions
{
    public const string SectionName = "Passkeys";

    /// <summary>
    /// Off by default. Disabled means the endpoints answer 404 and the client
    /// never offers the option.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The domain credentials are bound to. Either the host serving the app
    /// ("portal.example.com") or a registrable domain suffix of it
    /// ("example.com") — the latter lets one passkey work across every
    /// subdomain. Never a URL and never a port; those belong in Origins.
    ///
    /// Changing this orphans every passkey already registered, in either
    /// direction, so it is worth settling before anyone enrols.
    /// </summary>
    public string RelyingPartyId { get; set; } = "localhost";

    /// <summary>What the authenticator shows the user when prompting.</summary>
    public string RelyingPartyName { get; set; } = "claude-starter";

    /// <summary>
    /// Origins allowed to complete a ceremony, including scheme and port.
    /// </summary>
    public string[] Origins { get; set; } = ["http://localhost:5000"];
}

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
    /// The domain credentials are bound to — "example.com", never a URL and
    /// never a port. A passkey created under one relying party id cannot be
    /// used under another, so changing this orphans every existing passkey.
    /// </summary>
    public string RelyingPartyId { get; set; } = "localhost";

    /// <summary>What the authenticator shows the user when prompting.</summary>
    public string RelyingPartyName { get; set; } = "claude-starter";

    /// <summary>
    /// Origins allowed to complete a ceremony, including scheme and port.
    /// </summary>
    public string[] Origins { get; set; } = ["http://localhost:5000"];
}

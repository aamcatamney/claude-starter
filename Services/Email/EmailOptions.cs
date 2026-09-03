namespace claude_starter.Services.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>
    /// Off by default. With SMTP off nothing is sent, and email verification
    /// cannot be required — see <see cref="AuthOptions"/>.
    /// </summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Whether unverified users are refused a session. Forced to false when
    /// SMTP is disabled — requiring a verification nobody can send would lock
    /// every account out of the application permanently.
    /// </summary>
    public bool RequireEmailVerification { get; set; }

    /// <summary>
    /// Origin used to build links in emails, e.g. https://app.example.com.
    /// Falls back to the origin of the request that triggered the mail.
    /// </summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    public int BCryptWorkFactor { get; set; } = Services.Auth.BCryptPasswordHasher.DefaultWorkFactor;
}

using System.Diagnostics.Metrics;

namespace claude_starter.Services.Diagnostics;

/// <summary>
/// The measurements only this application can report. Request rate, latency and
/// GC come free from the ASP.NET Core and runtime instrumentation; these say
/// whether the things the application exists to do are working.
///
/// Instruments are always created. With metrics disabled nothing subscribes, and
/// an unobserved counter costs a branch — so endpoints need no conditional code.
/// </summary>
public sealed class AppMetrics
{
    public const string MeterName = "claude_starter.auth";

    private readonly Counter<long> _signIns;
    private readonly Counter<long> _registrations;
    private readonly Counter<long> _emails;
    private readonly Counter<long> _tokenRedemptions;

    public AppMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _signIns = meter.CreateCounter<long>(
            "auth.sign_in",
            unit: "{attempt}",
            description: "Sign-in attempts by outcome.");

        _registrations = meter.CreateCounter<long>(
            "auth.registration",
            unit: "{attempt}",
            description: "Registration attempts by outcome.");

        _emails = meter.CreateCounter<long>(
            "auth.email_sent",
            unit: "{email}",
            description: "Emails handed to the sender, by purpose.");

        _tokenRedemptions = meter.CreateCounter<long>(
            "auth.token_redemption",
            unit: "{attempt}",
            description: "Attempts to redeem an emailed link, by purpose and outcome.");
    }

    /// <param name="outcome">success, invalid-credentials, inactive or unverified.</param>
    public void SignIn(string outcome) =>
        _signIns.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    /// <param name="outcome">created, pending-verification, duplicate or invalid.</param>
    public void Registration(string outcome) =>
        _registrations.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void EmailSent(string purpose) =>
        _emails.Add(1, new KeyValuePair<string, object?>("purpose", purpose));

    public void TokenRedemption(string purpose, bool succeeded) =>
        _tokenRedemptions.Add(
            1,
            new KeyValuePair<string, object?>("purpose", purpose),
            new KeyValuePair<string, object?>("outcome", succeeded ? "redeemed" : "rejected"));
}

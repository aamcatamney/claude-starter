using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using claude_starter.Services.Email;

namespace claude_starter.IntegrationTests.Infrastructure;

public sealed record CapturedEmail(string To, string Subject, string Body)
{
    /// <summary>
    /// The token carried by the link in the body, which is the only place a
    /// test can get one — the database stores just its hash.
    /// </summary>
    public string? Token
    {
        get
        {
            var match = Regex.Match(Body, @"token=([A-Za-z0-9\-_%]+)");
            return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
        }
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<CapturedEmail> _sent = new();

    public IReadOnlyCollection<CapturedEmail> Sent => _sent.ToArray();

    public CapturedEmail? LastTo(string address) =>
        _sent.Where(e => string.Equals(e.To, address, StringComparison.OrdinalIgnoreCase)).LastOrDefault();

    public void Clear() => _sent.Clear();

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        _sent.Enqueue(new CapturedEmail(toAddress, subject, body));
        return Task.CompletedTask;
    }
}

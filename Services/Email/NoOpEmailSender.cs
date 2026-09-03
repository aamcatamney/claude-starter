namespace claude_starter.Services.Email;

/// <summary>
/// Used when SMTP is disabled. In Development it logs the message so the
/// verification and reset flows can be exercised without a mail server; in any
/// other environment it stays silent, because these bodies contain working
/// links and a log is not a place to put credentials.
/// </summary>
public sealed class NoOpEmailSender : IEmailSender
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(IHostEnvironment environment, ILogger<NoOpEmailSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "SMTP disabled. Would have sent to {Recipient}: {Subject}\n{Body}",
                toAddress,
                subject,
                body);
        }

        return Task.CompletedTask;
    }
}

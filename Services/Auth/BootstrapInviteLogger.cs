using Microsoft.Extensions.Options;
using claude_starter.Repositories;
using claude_starter.Services.Email;

namespace claude_starter.Services.Auth;

/// <summary>
/// Writes a link for creating the first administrator into the log, every time
/// the application starts while no account exists.
///
/// It stops the moment somebody registers, which is the point at which a
/// sign-up link in a log would stop being acceptable.
/// </summary>
public sealed class BootstrapInviteLogger : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapInviteService _invites;
    private readonly AuthOptions _auth;
    private readonly ILogger<BootstrapInviteLogger> _logger;

    public BootstrapInviteLogger(
        IServiceScopeFactory scopeFactory,
        BootstrapInviteService invites,
        IOptions<AuthOptions> auth,
        ILogger<BootstrapInviteLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _invites = invites;
        _auth = auth.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            if (await users.AnyAsync(cancellationToken))
            {
                return;
            }

            var link = _invites.BuildLink(_invites.Issue());

            _logger.LogWarning(
                "No accounts exist yet. Create the first administrator with this link, valid for {Days} days:\n\n    {Link}\n\n" +
                "It stops working as soon as an account exists. Restart to issue another.{Note}",
                BootstrapInviteService.Lifetime.TotalDays,
                link,
                _auth.AllowPublicRegistration
                    ? " Public registration is open, so /register also works without it."
                    : string.Empty);
        }
        catch (Exception ex)
        {
            // A missing invite must never stop the application booting.
            _logger.LogError(ex, "Could not issue a bootstrap invite.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using claude_starter.Repositories;

namespace claude_starter.Services.Auth;

/// <summary>
/// Deletes spent and expired email links once they are past retention. Without
/// this the table only ever grows: every reset request and every registration
/// leaves a row behind.
/// </summary>
public sealed class TokenCleanupService : BackgroundService
{
    /// <summary>
    /// How long a dead token is kept. Long enough to answer "was a reset link
    /// ever requested for this account?", which is what gets asked after a
    /// suspicious sign-in.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sweep on startup as well as on the timer: a host that restarts often
        // would otherwise never reach the first tick.
        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IUserTokenRepository>();

            var deleted = await tokens.DeleteDeadTokensAsync(Retention, ct);
            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} expired or spent email tokens.", deleted);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // A failed sweep must not take the host down; the next tick retries.
            _logger.LogError(ex, "Token cleanup sweep failed.");
        }
    }
}

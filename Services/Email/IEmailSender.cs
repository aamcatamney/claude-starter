namespace claude_starter.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default);
}

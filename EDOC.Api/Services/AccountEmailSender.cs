using System.Net;
using System.Net.Mail;

namespace EDOC.Api.Services;

public interface IAccountEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IConfiguration configuration) : IAccountEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
        && !string.IsNullOrWhiteSpace(configuration["Email:From"]);

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Account email delivery is not configured.");

        using var message = new MailMessage(from, recipient, subject, body);
        using var client = new SmtpClient(host, int.TryParse(configuration["Email:Port"], out var port) ? port : 587)
        {
            EnableSsl = !bool.TryParse(configuration["Email:EnableSsl"], out var ssl) || ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(configuration["Email:Username"])
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"])
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ModulithFoundry.Modules.Access.ExtensionPoints;

namespace ModulithFoundry.Modules.Access.Email;

internal sealed class SmtpEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.RecipientAddress));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            ToSecureSocketOptions(_options.Security),
            cancellationToken
        );
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(
                _options.Username,
                _options.Password!,
                cancellationToken
            );
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    private static SecureSocketOptions ToSecureSocketOptions(SmtpSecurity security) =>
        security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new ArgumentOutOfRangeException(
                nameof(security),
                security,
                "Unknown SMTP security mode."
            ),
        };
}

namespace ModulithFoundry.Modules.Access.ExtensionPoints;

public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

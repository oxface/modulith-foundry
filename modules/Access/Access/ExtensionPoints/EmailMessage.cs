namespace ModulithFoundry.Modules.Access.ExtensionPoints;

public sealed record EmailMessage(
    string RecipientAddress,
    string Subject,
    string TextBody,
    string HtmlBody);

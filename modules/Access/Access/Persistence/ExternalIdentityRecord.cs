namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class ExternalIdentityRecord
{
    internal const string IssuerSubjectConstraint = "ux_external_identities_issuer_subject";

    private ExternalIdentityRecord()
    {
        Issuer = null!;
        Subject = null!;
        User = null!;
    }

    internal ExternalIdentityRecord(
        Guid id,
        string issuer,
        string subject,
        User user,
        DateTimeOffset authenticatedAt
    )
    {
        Id = id;
        Issuer = issuer;
        Subject = subject;
        User = user;
        UserId = user.Id;
        LinkedAt = authenticatedAt;
        LastAuthenticatedAt = authenticatedAt;
    }

    internal Guid Id { get; private set; }

    internal string Issuer { get; private set; }

    internal string Subject { get; private set; }

    internal Guid UserId { get; private set; }

    internal User User { get; private set; }

    internal DateTimeOffset LinkedAt { get; private set; }

    internal DateTimeOffset LastAuthenticatedAt { get; private set; }

    internal void RecordAuthentication(DateTimeOffset authenticatedAt) =>
        LastAuthenticatedAt = authenticatedAt;
}

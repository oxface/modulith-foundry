using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class Invitation : IOrganizationOwned
{
    private readonly List<InvitationRoleAssignment> _roleAssignments = [];

    private Invitation()
    {
        RecipientEmail = null!;
        SecretDigest = null!;
    }

    private Invitation(
        Guid id,
        Guid organizationId,
        string recipientEmail,
        byte[] secretDigest,
        IReadOnlyCollection<string> roleIds,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
    {
        Id = id;
        OrganizationId = organizationId;
        RecipientEmail = recipientEmail;
        SecretDigest = secretDigest;
        Generation = 1;
        Status = InvitationStatus.Pending;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        _roleAssignments.AddRange(
            roleIds.Select(roleId => InvitationRoleAssignment.Create(id, organizationId, roleId))
        );
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal string RecipientEmail { get; private set; }

    internal byte[] SecretDigest { get; private set; }

    internal int Generation { get; private set; }

    internal InvitationStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset ExpiresAt { get; private set; }

    internal Guid? AcceptedByUserId { get; private set; }

    internal DateTimeOffset? AcceptedAt { get; private set; }

    internal IReadOnlyCollection<InvitationRoleAssignment> RoleAssignments => _roleAssignments;

    internal static Invitation Create(
        Guid id,
        Guid organizationId,
        InvitationEmailAddress recipientEmail,
        byte[] secretDigest,
        IReadOnlyCollection<string> roleIds,
        DateTimeOffset createdAt,
        TimeSpan lifetime
    ) =>
        new(
            id,
            organizationId,
            recipientEmail.Value,
            secretDigest,
            roleIds,
            createdAt,
            createdAt.Add(lifetime)
        );

    internal void Resend(byte[] secretDigest, DateTimeOffset resentAt, TimeSpan lifetime)
    {
        SecretDigest = secretDigest;
        Generation++;
        ExpiresAt = resentAt.Add(lifetime);
    }

    internal void Accept(Guid userId, DateTimeOffset acceptedAt)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = acceptedAt;
    }
}

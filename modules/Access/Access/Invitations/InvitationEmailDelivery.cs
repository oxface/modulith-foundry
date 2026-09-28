using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationEmailDelivery : IOrganizationOwned
{
    private InvitationEmailDelivery()
    {
        ProtectedPayload = null!;
    }

    private InvitationEmailDelivery(
        Guid id,
        Guid organizationId,
        Guid invitationId,
        int invitationGeneration,
        string protectedPayload,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        InvitationId = invitationId;
        InvitationGeneration = invitationGeneration;
        ProtectedPayload = protectedPayload;
        CreatedAt = createdAt;
        AvailableAt = createdAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid InvitationId { get; private set; }

    internal int InvitationGeneration { get; private set; }

    internal string? ProtectedPayload { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset AvailableAt { get; private set; }

    internal int AttemptCount { get; private set; }

    internal Guid? LeaseId { get; private set; }

    internal DateTimeOffset? LeaseExpiresAt { get; private set; }

    internal DateTimeOffset? SentAt { get; private set; }

    internal DateTimeOffset? SupersededAt { get; private set; }

    internal static InvitationEmailDelivery Create(
        Guid id,
        Invitation invitation,
        string protectedPayload,
        DateTimeOffset createdAt) =>
        new(
            id,
            invitation.OrganizationId,
            invitation.Id,
            invitation.Generation,
            protectedPayload,
            createdAt);

    internal void Supersede(DateTimeOffset supersededAt)
    {
        SupersededAt = supersededAt;
        ProtectedPayload = null;
        LeaseId = null;
        LeaseExpiresAt = null;
    }

    internal void MarkSent(DateTimeOffset sentAt)
    {
        SentAt = sentAt;
        ProtectedPayload = null;
        LeaseId = null;
        LeaseExpiresAt = null;
    }

    internal void ReleaseAfterFailure(DateTimeOffset availableAt)
    {
        AttemptCount++;
        AvailableAt = availableAt;
        LeaseId = null;
        LeaseExpiresAt = null;
    }
}

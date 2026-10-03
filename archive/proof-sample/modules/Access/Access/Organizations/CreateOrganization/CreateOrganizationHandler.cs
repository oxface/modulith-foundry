using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Organizations.CreateOrganization;

internal sealed class CreateOrganizationHandler(AccessDbContext context, TimeProvider timeProvider)
    : IOrganizationCreation
{
    public async Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTimeOffset createdAt = timeProvider.GetUtcNow();
        Organization organization;
        try
        {
            organization = Organization.Create(
                Guid.CreateVersion7(createdAt),
                command.Name,
                command.ProposedSlug,
                createdAt
            );
        }
        catch (InvalidOrganizationNameException exception)
        {
            return new CreateOrganizationResult.InvalidName(exception.Message);
        }
        catch (InvalidOrganizationSlugException exception)
        {
            return new CreateOrganizationResult.InvalidSlug(exception.Message);
        }

        bool userExists = await context.Users.AnyAsync(
            user => user.Id == command.ActorUserId.Value,
            cancellationToken
        );
        if (!userExists)
        {
            throw new InvalidOperationException(
                $"Product user '{command.ActorUserId.Value}' does not exist."
            );
        }

        Membership membership = Membership.CreateInitialAdministrator(
            Guid.CreateVersion7(createdAt),
            organization.Id,
            command.ActorUserId.Value,
            createdAt
        );
        AccessAuditEntry audit = OrganizationAuditEntries.Created(
            organization,
            command.ActorUserId.Value,
            createdAt
        );

        context.Organizations.Add(organization);
        context.Memberships.Add(membership);
        context.AuditEntries.Add(audit);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUnavailableSlug(exception))
        {
            return new CreateOrganizationResult.SlugUnavailable(organization.Slug.Value);
        }

        return new CreateOrganizationResult.Created(
            new OrganizationMembership(
                new OrganizationId(organization.Id),
                organization.Name,
                organization.Slug.Value,
                [SystemRoleIds.OrganizationAdministrator]
            )
        );
    }

    private static bool IsUnavailableSlug(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_organizations_slug",
            };
}

using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Invitations;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class AccessDbContext(
    DbContextOptions<AccessDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor) : DbContext(options)
{
    internal const string Schema = "access";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<User> Users => Set<User>();

    internal DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>();

    internal DbSet<Organization> Organizations => Set<Organization>();

    internal DbSet<Membership> Memberships => Set<Membership>();

    internal DbSet<MembershipRoleAssignment> MembershipRoleAssignments =>
        Set<MembershipRoleAssignment>();

    internal DbSet<AccessAuditEntry> AuditEntries => Set<AccessAuditEntry>();

    internal DbSet<Invitation> Invitations => Set<Invitation>();

    internal DbSet<InvitationEmailDelivery> InvitationEmailDeliveries =>
        Set<InvitationEmailDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(
            OrganizationScopeFilter,
            entity => CurrentOrganizationId.HasValue
                && entity.OrganizationId == CurrentOrganizationId);
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value;

}

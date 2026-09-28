using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Invitations;
using ModulithFoundry.Modules.Access.Organizations;

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
        ApplyOrganizationFilters(modelBuilder);
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value;

    private void ApplyOrganizationFilters(ModelBuilder modelBuilder)
    {
        foreach (Type entityType in modelBuilder.Model.GetEntityTypes()
            .Select(metadata => metadata.ClrType)
            .Where(typeof(IOrganizationOwned).IsAssignableFrom))
        {
            ParameterExpression entity = Expression.Parameter(entityType, "entity");
            MemberExpression currentOrganizationId = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentOrganizationId));
            BinaryExpression filter = Expression.AndAlso(
                Expression.Property(
                    currentOrganizationId,
                    nameof(Nullable<Guid>.HasValue)),
                Expression.Equal(
                    Expression.Convert(
                        Expression.Property(entity, nameof(IOrganizationOwned.OrganizationId)),
                        typeof(Guid?)),
                    currentOrganizationId));

            modelBuilder.Entity(entityType).HasQueryFilter(
                OrganizationScopeFilter,
                Expression.Lambda(filter, entity));
        }
    }
}

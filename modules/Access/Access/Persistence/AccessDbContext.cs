using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
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
                    Expression.Property(entity, nameof(IOrganizationOwned.OrganizationId)),
                    Expression.Property(
                        currentOrganizationId,
                        nameof(Nullable<Guid>.Value))));

            modelBuilder.Entity(entityType).HasQueryFilter(
                OrganizationScopeFilter,
                Expression.Lambda(filter, entity));
        }
    }
}

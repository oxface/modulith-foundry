using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Customers;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Sales.Persistence;

internal sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor) : DbContext(options)
{
    internal const string Schema = "sales";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<Customer> Customers => Set<Customer>();
    internal DbSet<SalesAuditEntry> AuditEntries => Set<SalesAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(OrganizationScopeFilter,
            entity => CurrentOrganizationId.HasValue && entity.OrganizationId == CurrentOrganizationId);
    }

    private Guid? CurrentOrganizationId => organizationContextAccessor.OrganizationContext?.OrganizationId.Value;
}

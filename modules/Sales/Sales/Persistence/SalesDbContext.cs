using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Customers;
using ModulithFoundry.Modules.Sales.Orders;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Orders.Persistence;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Sales.Persistence;

internal sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor
) : DbContext(options)
{
    internal const string Schema = "sales";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<Customer> Customers => Set<Customer>();
    internal DbSet<SalesApprovalAuthority> ApprovalAuthorities => Set<SalesApprovalAuthority>();
    internal DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    internal DbSet<SalesOrderActivity> OrderActivity => Set<SalesOrderActivity>();
    internal DbSet<SalesOrderNumber> SalesOrderNumbers => Set<SalesOrderNumber>();
    internal DbSet<SalesAuditEntry> AuditEntries => Set<SalesAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(
            OrganizationScopeFilter,
            entity =>
                CurrentOrganizationId.HasValue && entity.OrganizationId == CurrentOrganizationId
        );
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value;
}
